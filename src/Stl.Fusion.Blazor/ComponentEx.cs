using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Stl.Fusion.Blazor
{
    public static class ComponentEx
    {
        private static readonly MethodInfo StateHasChangedMethod =
            typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly MethodInfo InvokeAsyncMethod =
            typeof(ComponentBase).GetMethod("InvokeAsync",
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: new Type[] { typeof(Func<Task>) }, modifiers: null)!;

        /// <summary>
        /// Calls StateHasChanged in the component's SyncContext
        /// </summary>
        public static void StateHasChanges(this ComponentBase component)
        {
            Func<Task> stateHasChangedAction = async () => {
                // https://github.com/servicetitan/Stl.Fusion/issues/255
                // Yielding before invoking component.StateHasChanged() helps prevent exceptions when the 
                // renderer and the component are in the process of being disposed.
                await Task.Yield();
                try {
                    StateHasChangedMethod.Invoke(component, Array.Empty<object>());
                }
                catch (ObjectDisposedException) {
                    // ObjectDisposedException still occasionally happens on page reload.
                    // Its always in the context of disposing components so makes sense to just ignore.
                }
            };

            // https://docs.microsoft.com/en-us/aspnet/core/blazor/components/rendering?view=aspnetcore-5.0
            // Call StateHasChanged() via InvokeAsync() to ensure the correct synchronization context is used
            // when the StateChange call originates from a context different from the component (e.g. a Fusion state update)
            var invokeAsyncParameters = new object[] { stateHasChangedAction };
            InvokeAsyncMethod.Invoke(component, invokeAsyncParameters);
        }
    }
}
