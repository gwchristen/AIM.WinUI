// File: Services/Ui.cs
using System;
using Microsoft.UI.Dispatching;

namespace AIM.WinUI.Services
{
    public static class Ui
    {
        public static DispatcherQueue? Dispatcher { get; set; }

        public static void Enqueue(Action action)
        {
            var dq = Dispatcher;
            if (dq is not null)
            {
                dq.TryEnqueue(() => action());
            }
            // Else: not initialized yet — if needed, you can buffer actions here.
        }
    }
}
