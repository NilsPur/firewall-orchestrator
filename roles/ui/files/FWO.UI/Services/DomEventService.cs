using Microsoft.JSInterop;
using System.Diagnostics;

namespace FWO.Ui.Services
{
	public class DomEventService
	{
		public event Action<string>? OnGlobalScroll;
		public event Action<string>? OnGlobalClick;
		public event Action? OnGlobalResize;
        public event Action<int>? OnNavbarHeightChanged;

        public bool Initialized { get; private set; } = false;

		[JSInvokable]
		public void InvokeOnGlobalScroll(string elementId)
		{
			OnGlobalScroll?.Invoke(elementId ?? "");
		}

		[JSInvokable]
		public void InvokeOnGlobalResize()
		{
			OnGlobalResize?.Invoke();
		}

		[JSInvokable]
		public void InvokeOnGlobalClick(string elementId)
		{
			OnGlobalClick?.Invoke(elementId ?? "");
		}

        [JSInvokable]
        public void InvokeNavbarHeightChanged(int height)
        {
            OnNavbarHeightChanged?.Invoke(height);
        }

        public async Task Initialize(IJSRuntime runtime)
		{
			if (!Initialized)
			{
                try
                {
                    await runtime.InvokeVoidAsync("globalScroll", DotNetObjectReference.Create(this));
                    await runtime.InvokeVoidAsync("globalResize", DotNetObjectReference.Create(this));
                    await runtime.InvokeVoidAsync("globalClick", DotNetObjectReference.Create(this));
                    await runtime.InvokeVoidAsync("observeNavbarHeight", DotNetObjectReference.Create(this));
                    Initialized = true;
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }				
			}
		}
	}
}
