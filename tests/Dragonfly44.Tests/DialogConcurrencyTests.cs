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
        public bool DismissActiveDialogCalled { get; private set; }

        public void EnqueueResponse(Func<DialogOptions, Task<DialogResult>> responseFunc)
        {
            _responses.Enqueue(responseFunc);
        }

        public async Task<DialogResult> ShowDialogAsync(DialogOptions options, CancellationToken cancellationToken = default)
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

        public void DismissActiveDialog()
        {
            DismissActiveDialogCalled = true;
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
    public async Task ShowCustomDialogAsync_MultipleConcurrentRequests_GuaranteedFifoExecution()
    {
        var presenter = new MockDialogPresenter();
        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        var tcsList = new List<TaskCompletionSource<DialogResult>>();
        for (int i = 0; i < 5; i++)
        {
            var tcs = new TaskCompletionSource<DialogResult>();
            tcsList.Add(tcs);
            presenter.EnqueueResponse(_ => tcs.Task);
        }

        // Enqueue 5 requests concurrently in order 1..5
        var tasks = new List<Task<DialogResult>>();
        for (int i = 1; i <= 5; i++)
        {
            tasks.Add(service.ShowConfirmationAsync($"Title {i}", $"Message {i}"));
        }

        // Release one by one and assert strict sequential ordering
        for (int i = 0; i < 5; i++)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2);
            while (presenter.PresentedDialogs.Count < i + 1 && DateTime.UtcNow < timeout)
            {
                await Task.Delay(10);
            }
            presenter.PresentedDialogs.Count.Should().Be(i + 1, $"Dialog {i + 1} should be displayed next");
            presenter.PresentedDialogs[i].Title.Should().Be($"Title {i + 1}");

            tcsList[i].SetResult(DialogResult.Primary);
            var res = await tasks[i];
            res.Should().Be(DialogResult.Primary);
        }

        presenter.PresentedDialogs.Select(d => d.Title)
            .Should().Equal("Title 1", "Title 2", "Title 3", "Title 4", "Title 5");
    }

    [Fact]
    public async Task ShowCustomDialogAsync_WhenCallerCancelsWhileQueued_ShouldCancelSafelyAndAdvanceQueue()
    {
        var presenter = new MockDialogPresenter();
        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        var tcs1 = new TaskCompletionSource<DialogResult>();
        var tcs3 = new TaskCompletionSource<DialogResult>();

        presenter.EnqueueResponse(_ => tcs1.Task);
        presenter.EnqueueResponse(_ => tcs3.Task);

        using var cts2 = new CancellationTokenSource();

        var task1 = service.ShowConfirmationAsync("Dialog 1", "Message 1");
        var task2 = service.ShowConfirmationAsync("Dialog 2", "Message 2", cancellationToken: cts2.Token);
        var task3 = service.ShowConfirmationAsync("Dialog 3", "Message 3");

        await Task.Yield();

        // Cancel dialog 2 while it is waiting in the queue
        cts2.Cancel();

        // task 2 should resolve immediately with Cancel
        var result2 = await task2;
        result2.Should().Be(DialogResult.Cancel);

        // Now complete dialog 1
        tcs1.SetResult(DialogResult.Primary);
        var result1 = await task1;
        result1.Should().Be(DialogResult.Primary);

        // Dialog 3 should now be presented directly, skipping dialog 2
        await Task.Yield();
        tcs3.SetResult(DialogResult.Primary);
        var result3 = await task3;
        result3.Should().Be(DialogResult.Primary);

        presenter.PresentedDialogs.Select(d => d.Title).Should().Equal("Dialog 1", "Dialog 3");
    }

    [Fact]
    public async Task ShowCustomDialogAsync_WhenActiveRequestIsCancelled_ShouldDismissActivePresenterDialog()
    {
        var presenter = new MockDialogPresenter();
        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        var tcs1 = new TaskCompletionSource<DialogResult>();
        presenter.EnqueueResponse(_ => tcs1.Task);

        using var cts = new CancellationTokenSource();
        var task = service.ShowConfirmationAsync("Active Dialog", "Message", cancellationToken: cts.Token);

        await Task.Yield();
        presenter.DismissActiveDialogCalled.Should().BeFalse();

        // Act: cancel the active dialog
        cts.Cancel();

        // Assert: DismissActiveDialog should be invoked on presenter
        presenter.DismissActiveDialogCalled.Should().BeTrue("Cancellation of an active request must physically dismiss the active dialog");
        var result = await task;
        result.Should().Be(DialogResult.Cancel);
    }

    [Fact]
    public async Task ShowCustomDialogAsync_WhenPresenterThrows_ShouldNotLeaveSubsequentCallersWaiting()
    {
        var presenter = new MockDialogPresenter();
        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        // First dialog throws
        presenter.EnqueueResponse(_ => throw new InvalidOperationException("Simulated UI host crash"));
        // Second dialog succeeds
        presenter.EnqueueResponse(_ => Task.FromResult(DialogResult.Primary));

        var task1 = service.ShowConfirmationAsync("Failing Dialog", "Message");
        var task2 = service.ShowConfirmationAsync("Succeeding Dialog", "Message");

        var result1 = await task1;
        result1.Should().Be(DialogResult.Cancel, "Failed dialog presentation must return Cancel safely without throwing unhandled exception");

        var result2 = await task2;
        result2.Should().Be(DialogResult.Primary, "Subsequent dialog in FIFO queue must be processed cleanly");
    }

    [Fact]
    public async Task CancelAllPendingRequests_OnShutdown_ShouldDismissActiveAndCancelAllQueuedCallers()
    {
        var presenter = new MockDialogPresenter();
        var service = new DialogService(NullLogger<DialogService>.Instance);
        service.RegisterPresenter(presenter);

        var tcs1 = new TaskCompletionSource<DialogResult>();
        presenter.EnqueueResponse(_ => tcs1.Task);

        var task1 = service.ShowConfirmationAsync("Dialog 1", "Message 1");
        var task2 = service.ShowConfirmationAsync("Dialog 2", "Message 2");
        var task3 = service.ShowConfirmationAsync("Dialog 3", "Message 3");

        await Task.Yield();

        // Shutdown called: cancel all active and pending
        service.CancelAllPendingRequests();

        presenter.DismissActiveDialogCalled.Should().BeTrue("Active dialog must be dismissed upon CancelAllPendingRequests");

        var result1 = await task1;
        var result2 = await task2;
        var result3 = await task3;

        result1.Should().Be(DialogResult.Cancel);
        result2.Should().Be(DialogResult.Cancel);
        result3.Should().Be(DialogResult.Cancel);

        // New requests after shutdown should be rejected immediately
        var newTask = await service.ShowConfirmationAsync("Post-Shutdown", "Message");
        newTask.Should().Be(DialogResult.Cancel, "New dialog requests after shutdown must be rejected immediately");
    }

    [Fact]
    public async Task ShowCustomDialogAsync_WithoutPresenter_ShouldSafelyReturnCancel()
    {
        var service = new DialogService(NullLogger<DialogService>.Instance);

        var act = async () => await service.ShowMessageAsync("Unregistered", "Should not hang");
        await act.Should().NotThrowAsync();
    }
}
