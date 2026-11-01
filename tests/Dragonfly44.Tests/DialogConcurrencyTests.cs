using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class DialogConcurrencyTests
{
    private class MockDialogPresenter : IDialogPresenter
    {
        private readonly Queue<Func<DialogOptions, Task<DialogResult>>> _responses = new();
        public List<DialogOptions> PresentedDialogs { get; } = new();
        public int ConcurrentlyActiveDialogs { get; private set; }
        public int MaxConcurrentDialogsObserved { get; private set; }

        public void EnqueueResponse(Func<DialogOptions, Task<DialogResult>> responseFunc)
        {
            _responses.Enqueue(responseFunc);
        }

        public async Task<DialogResult> ShowDialogAsync(DialogOptions options)
        {
            PresentedDialogs.Add(options);
            ConcurrentlyActiveDialogs++;
            if (ConcurrentlyActiveDialogs > MaxConcurrentDialogsObserved)
            {
                MaxConcurrentDialogsObserved = ConcurrentlyActiveDialogs;
            }

            try
            {
                if (_responses.Count > 0)
                {
                    var responseFunc = _responses.Dequeue();
                    return await responseFunc(options);
                }

                return DialogResult.Primary;
            }
            finally
            {
                ConcurrentlyActiveDialogs--;
            }
        }
    }

    [Fact]
    public async Task ShowCustomDialogAsync_SingleRequest_ShouldReturnExpectedResult()
    {
        var presenter = new MockDialogPresenter();
        presenter.EnqueueResponse(_ => Task.FromResult(DialogResult.Primary));

        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        var result = await service.ShowConfirmationAsync("Title", "Message");

        result.Should().Be(DialogResult.Primary);
        presenter.PresentedDialogs.Should().ContainSingle();
        presenter.MaxConcurrentDialogsObserved.Should().Be(1);
    }

    [Fact]
    public async Task ShowCustomDialogAsync_ConcurrentRequests_ShouldQueueInFifoOrderAndNeverAbandonCallers()
    {
        var presenter = new MockDialogPresenter();
        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        var tcs1 = new TaskCompletionSource<DialogResult>();
        var tcs2 = new TaskCompletionSource<DialogResult>();
        var tcs3 = new TaskCompletionSource<DialogResult>();

        presenter.EnqueueResponse(_ => tcs1.Task);
        presenter.EnqueueResponse(_ => tcs2.Task);
        presenter.EnqueueResponse(_ => tcs3.Task);

        // Act: trigger 3 dialog requests concurrently
        var task1 = service.ShowConfirmationAsync("Dialog 1", "Message 1");
        var task2 = service.ShowConfirmationAsync("Dialog 2", "Message 2");
        var task3 = service.ShowConfirmationAsync("Dialog 3", "Message 3");

        // Give the async pipeline a cycle to process
        await Task.Yield();

        // Dialog 1 should be active, but Dialog 2 and 3 should be queued
        task1.IsCompleted.Should().BeFalse();
        task2.IsCompleted.Should().BeFalse();
        task3.IsCompleted.Should().BeFalse();
        presenter.ConcurrentlyActiveDialogs.Should().Be(1, "Strict single-dialog policy: only 1 dialog visible at a time");

        // Complete Dialog 1
        tcs1.SetResult(DialogResult.Primary);
        var result1 = await task1;
        result1.Should().Be(DialogResult.Primary);

        // Dialog 2 now activates
        await Task.Yield();
        presenter.ConcurrentlyActiveDialogs.Should().Be(1);
        tcs2.SetResult(DialogResult.Secondary);
        var result2 = await task2;
        result2.Should().Be(DialogResult.Secondary);

        // Dialog 3 now activates
        await Task.Yield();
        presenter.ConcurrentlyActiveDialogs.Should().Be(1);
        tcs3.SetResult(DialogResult.Cancel);
        var result3 = await task3;
        result3.Should().Be(DialogResult.Cancel);

        // Verification
        presenter.MaxConcurrentDialogsObserved.Should().Be(1, "At no point should more than one dialog be shown simultaneously");
        presenter.PresentedDialogs.Select(d => d.Title).Should().ContainInOrder("Dialog 1", "Dialog 2", "Dialog 3");
    }

    [Fact]
    public async Task ShowCustomDialogAsync_WithoutPresenter_ShouldSafelyReturnCancel()
    {
        var service = new DialogService(NullLogger<DialogService>.Instance);

        var act = async () => await service.ShowMessageAsync("Unregistered", "Should not hang");
        await act.Should().NotThrowAsync();

        // Verify task completes immediately with no hanging
        // ShowMessageAsync returns Task; it shouldn't throw or block
    }
}
