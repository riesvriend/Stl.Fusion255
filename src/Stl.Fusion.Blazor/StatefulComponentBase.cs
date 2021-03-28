using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Stl.Internal;

namespace Stl.Fusion.Blazor
{
    public abstract class StatefulComponentBase : ComponentBase, IDisposable
    {
        [Inject]
        protected IServiceProvider Services { get; set; } = null!;
        protected IStateFactory StateFactory => Services.StateFactory();
        protected bool OwnsState { get; set; } = true;
        protected internal abstract IState UntypedState { get; }
        protected Action<IState, StateEventKind> StateChanged { get; set; }
        protected StateEventKind StateHasChangedTriggers { get; set; } = StateEventKind.Updated;

        public bool IsLoading => UntypedState == null! || UntypedState.Snapshot.UpdateCount == 0;
        public bool IsUpdating => UntypedState == null! || UntypedState.Snapshot.IsUpdating;
        public bool IsUpdatePending => UntypedState == null! || UntypedState.Snapshot.Computed.IsInvalidated();

        protected StatefulComponentBase()
        {
			// The async modifier in the lambda passed to InvokeAsync below
			//  is needed to prevent occasional ObjectDisposedExceptions:
			// "Cannot process pending renders after the renderer has been disposed".
			// Without the lambda being really async, StateHasChanged may synchronously execute an actual render immediately.
			// Appears related to this issue https://github.com/dotnet/aspnetcore/issues/22159 
			// To reproduce:
			// - Uncomment the block and comment out the current block with the asynch lambda
			// - navigate from https://localhost:5001/todo to https://localhost:5001/todo2 
			//   where todo2 is a literal copy of the todopage.razor, with the route changed to /todo2
			
			//StateChanged = (state, eventKind) => InvokeAsync(() => {
			//	if ((eventKind & StateHasChangedTriggers) != 0) {
			//		StateHasChanged();
			//	}
			//});

			StateChanged = (state, eventKind) => InvokeAsync(async () => {
				if ((eventKind & StateHasChangedTriggers) != 0) {
                    await Task.Yield();
					StateHasChanged();
				}
			});
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
