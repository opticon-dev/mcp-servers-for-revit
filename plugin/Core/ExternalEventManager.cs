using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// 외부 이벤트의 생성과 생명주기를 관리합니다
    /// Manages the creation and lifecycle of external events.
    /// </summary>
    public class ExternalEventManager
    {
        private static ExternalEventManager _instance;
        private Dictionary<string, ExternalEventWrapper> _events = new Dictionary<string, ExternalEventWrapper>();
        private bool _isInitialized = false;
        private UIApplication _uiApp;
        private ILogger _logger;

        /// <summary>
        /// Manages the creation and lifecycle of external events.
        /// </summary>
        public static ExternalEventManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ExternalEventManager();
                return _instance;
            }
        }

        private ExternalEventManager() { }

        public void Initialize(UIApplication uiApp, ILogger logger)
        {
            _uiApp = uiApp;
            _logger = logger;
            _isInitialized = true;
        }

        /// <summary>
        /// 외부 이벤트를 가져오거나 생성합니다
        /// Obtain or create external events.
        /// </summary>
        public ExternalEvent GetOrCreateEvent(IWaitableExternalEventHandler handler, string key)
        {
            if (!_isInitialized)
                throw new InvalidOperationException($"{nameof(ExternalEventManager)}초기화되지 않음\n{nameof(ExternalEventManager)}has not been initialized.");

            // 이미 존재하고 핸들러가 일치하면 바로 반환합니다
            // If it exists and the processor matches, return directly.
            if (_events.TryGetValue(key, out var wrapper) &&
                wrapper.Handler == handler)
            {
                return wrapper.Event;
            }

            // UI 스레드에서 이벤트를 생성해야 합니다
            // You need to create events in the UI thread. 
            ExternalEvent externalEvent = null;

            // 활성 문서의 컨텍스트를 사용해 이벤트 생성 작업을 수행합니다
            // Perform the operation that created the event using the context of the active document.
            _uiApp.ActiveUIDocument.Document.Application.ExecuteCommand(
                (uiApp) => {
                    externalEvent = ExternalEvent.Create(handler);
                }
            );

            if (externalEvent == null)
                throw new InvalidOperationException("외부 이벤트를 생성할 수 없음\nUnable to create external events.");

            // 이벤트를 저장합니다
            // Storage events.
            _events[key] = new ExternalEventWrapper
            {
                Event = externalEvent,
                Handler = handler
            };

            _logger.Info($"키 {key}에 대한 새 외부 이벤트를 생성했습니다\nCreated a new external event for key {key}.");

            return externalEvent;
        }

        /// <summary>
        /// <para>이벤트 캐시를 지웁니다</para>
        /// <para>Clears the event cache.</para>
        /// </summary>
        public void ClearEvents()
        {
            _events.Clear();
        }

        private class ExternalEventWrapper
        {
            public ExternalEvent Event { get; set; }
            public IWaitableExternalEventHandler Handler { get; set; }
        }
    }
}

namespace Autodesk.Revit.DB
{
    public static class ApplicationExtensions
    {
        public delegate void CommandDelegate(UIApplication uiApp);

        /// <summary>
        /// <para>Revit 컨텍스트에서 명령을 실행합니다</para>
        /// <para>Execute commands in the Revit context.</para>
        /// </summary>
        public static void ExecuteCommand(this Autodesk.Revit.ApplicationServices.Application app, CommandDelegate command)
        {
            // 이 메서드는 Revit 컨텍스트에서 호출되므로 ExternalEvent를 안전하게 생성할 수 있습니다
            // This method is called in the Revit context and can safely create an ExternalEvent.
            command?.Invoke(new UIApplication(app));
        }
    }
}
