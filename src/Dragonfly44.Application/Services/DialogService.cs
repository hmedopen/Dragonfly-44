using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class DialogService : IDialogService
{
    private sealed class DialogRequest
    {
        public DialogOptions Options { get; }
        public TaskCompletionSource<DialogResult> CompletionSource { get; }
        public CancellationToken CancellationToken { get; }
        public CancellationTokenRegistration? Registration { get; set; }

        public DialogRequest(DialogOptions options, TaskCompletionSource<DialogResult> tcs, CancellationToken cancellationToken)
        {
            Options = options;
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    private readonly Queue<DialogRequest> _requestQueue = new();
    private readonly object _syncRoot = new();
    private bool _isProcessing;
    private bool _isShutdown;

    private readonly ILogger<DialogService> _logger;
    private IDialogPresenter? _presenter;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    public void RegisterPresenter(IDialogPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _logger.LogInformation("IDialogPresenter registered with DialogService.");
    }

    public async Task<DialogResult> ShowConfirmationAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        bool isDestructive = false,
        CancellationToken cancellationToken = default)
    {
        var options = new DialogOptions(
            Title: title,
            Message: message,
            PrimaryButtonText: confirmText,
            CloseButtonText: cancelText,
            IsDestructive: isDestructive);

        return await ShowCustomDialogAsync(options, cancellationToken);
    }

    public async Task ShowMessageAsync(
        string title,
        string message,
        string closeText = "OK",
        CancellationToken cancellationToken = default)
    {
        var options = new DialogOptions(
            Title: title,
            Message: message,
            PrimaryButtonText: closeText,
            CloseButtonText: null);

        await ShowCustomDialogAsync(options, cancellationToken);
    }

    public async Task<DialogResult> ShowCustomDialogAsync(
        DialogOptions options,
        CancellationToken cancellationToken = default)
    {
        if (_isShutdown)
        {
            _logger.LogWarning("Dialog presentation requested after DialogService shutdown. Returning Cancel.");
            return DialogResult.Cancel;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return DialogResult.Cancel;
        }

        var tcs = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new DialogRequest(options, tcs, cancellationToken);

        if (cancellationToken.CanBeCanceled)
        {
            request.Registration = cancellationToken.Register(() =>
            {
                request.CompletionSource.TrySetResult(DialogResult.Cancel);
            });
        }

        lock (_syncRoot)
        {
            if (_isShutdown)
            {
                request.Registration?.Dispose();
                request.CompletionSource.TrySetResult(DialogResult.Cancel);
                return DialogResult.Cancel;
            }

            _requestQueue.Enqueue(request);

            if (!_isProcessing)
            {
                _isProcessing = true;
                _ = ProcessQueueAsync();
            }
        }

        return await tcs.Task;
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            DialogRequest currentRequest;
            lock (_syncRoot)
            {
                if (_requestQueue.Count == 0 || _isShutdown)
                {
                    _isProcessing = false;
                    break;
                }

                currentRequest = _requestQueue.Dequeue();
            }

            // If cancelled while queued, complete with Cancel and advance to next item
            if (currentRequest.CancellationToken.IsCancellationRequested)
            {
                currentRequest.Registration?.Dispose();
                currentRequest.CompletionSource.TrySetResult(DialogResult.Cancel);
                continue;
            }

            if (_presenter == null)
            {
                _logger.LogWarning("Dialog presentation requested without a registered IDialogPresenter. Returning Cancel.");
                currentRequest.Registration?.Dispose();
                currentRequest.CompletionSource.TrySetResult(DialogResult.Cancel);
                continue;
            }

            try
            {
                _logger.LogInformation("Presenting queued dialog: '{Title}'", currentRequest.Options.Title);
                var result = await _presenter.ShowDialogAsync(currentRequest.Options);
                currentRequest.CompletionSource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while presenting dialog: '{Title}'. Returning Cancel.", currentRequest.Options.Title);
                currentRequest.CompletionSource.TrySetResult(DialogResult.Cancel);
            }
            finally
            {
                currentRequest.Registration?.Dispose();
            }
        }
    }

    public void CancelAllPendingRequests()
    {
        lock (_syncRoot)
        {
            _isShutdown = true;
            _logger.LogInformation("Cancelling all pending dialog requests due to shutdown/reset.");

            while (_requestQueue.Count > 0)
            {
                var req = _requestQueue.Dequeue();
                req.Registration?.Dispose();
                req.CompletionSource.TrySetResult(DialogResult.Cancel);
            }

            _isProcessing = false;
        }
    }
}
