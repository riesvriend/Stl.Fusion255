using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Stl.Internal;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Stl.Fusion.Blazor
{
    public abstract class StatefulComponentBase : ComponentBase, IDisposable, IHandleEvent
    {
        [Inject]
        protected IServiceProvider Services { get; set; } = null!;
        protected IStateFactory StateFactory => Services.StateFactory();
        protected bool OwnsState { get; set; } = true;

        // This flag allows components to suppress the call to StateHasChanged()
        // normally fired by Blazor on each event.
        // The default rerender can cause screen flicker, when "controlled Components" such as text boxes
        // get rerendered with their current (old) state, after the user has typed text, triggered a click event, 
        // that initiated a state update that is only be reflected in the State after a delay
        // https://github.com/dotnet/aspnetcore/issues/18919#issuecomment-735779810 
        protected bool RerenderOnEvents { get; set; } = true;
        protected internal abstract IState UntypedState { get; }
        protected Action<IState, StateEventKind> StateChanged { get; set; }
        protected StateEventKind StateHasChangedTriggers { get; set; } = StateEventKind.Updated;

        public bool IsLoading => UntypedState == null! || UntypedState.Snapshot.UpdateCount == 0;
        public bool IsUpdating => UntypedState == null! || UntypedState.Snapshot.IsUpdating;
        public bool IsUpdatePending => UntypedState == null! || UntypedState.Snapshot.Computed.IsInvalidated();

        private static long GlobalRenderCount = 0;

        protected StatefulComponentBase()
        {
            StateChanged = (state, eventKind) => {
                if ((eventKind & StateHasChangedTriggers) != 0) {
                    Debug.WriteLine($"Calling StateHasChanged. Kind: {eventKind}. State: {state.UnsafeValue}");
                    this.StateHasChanges();
                }
            };
        }

        Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
        {
            if (RerenderOnEvents)
                StateHasChanged();

            return callback.InvokeAsync(arg);
        }

        protected override void OnAfterRender(bool firstRender)
        {
            Interlocked.Increment(ref GlobalRenderCount);
            Debug.WriteLine($"Rendered {GetType().Name}. Render count: {GlobalRenderCount}");
            base.OnAfterRender(firstRender);
        }

        public virtual void Dispose()
        {
            UntypedState.RemoveEventHandler(StateEventKind.All, StateChanged);
            if (OwnsState && UntypedState is IDisposable d)
                d.Dispose();
        }
    }

    public abstract class StatefulComponentBase<TState> : StatefulComponentBase, IDisposable
        where TState : class, IState
    {
        private TState? _state;

        protected internal override IState UntypedState => State;

        protected internal TState State {
            get => _state!;
            set {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (_state == value)
                    return;
                if (_state != null)
                    throw Errors.AlreadyInitialized(nameof(State));
                _state = value;
            }
        }

        protected override void OnInitialized()
        {
            // ReSharper disable once ConstantNullCoalescingCondition
            State ??= CreateState();
            UntypedState.AddEventHandler(StateEventKind.All, StateChanged);
        }

        protected virtual TState CreateState()
            => Services.GetRequiredService<TState>();
    }
}
