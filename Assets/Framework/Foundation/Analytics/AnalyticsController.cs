using System;
using System.Collections.Generic;
using System.Text;
using Framework.Foundation.Initialization;
using Framework.Foundation.Logger;
using VContainer;

namespace Framework.Foundation.Analytics
{
    [AutoRegistration(Lifetime.Singleton)]
    public class AnalyticsController : IAnalyticsController
    {
        private readonly Dictionary<Type, IAnalyticsService> _analyticsServices = new();

        [Inject] private readonly IReadOnlyList<IAnalyticsService> _injectedAnalyticsServices;
        [Inject] private readonly ILogChannelFactory _logChannelFactory;

        private ILogChannel _logger;

        // [Inject] на этом ctor обязателен: рядом есть internal-шов с параметрами, а VContainer
        // без явной пометки выбрал бы конструктор с наибольшим числом параметров (TypeAnalyzer).
        [Inject]
        public AnalyticsController()
        {
        }

        // Тестовый шов: в проде поля и Init заполняет VContainer.
        internal AnalyticsController(IReadOnlyList<IAnalyticsService> analyticsServices, ILogChannel logger)
        {
            _injectedAnalyticsServices = analyticsServices;
            _logger = logger;
            InitServices();
        }

        // Логгер берётся здесь, а не через [AutoLogger]: у класса уже есть свой [Inject]-метод,
        // а порядок вызова нескольких [Inject]-методов VContainer не определяет.
        [Inject]
        private void Init()
        {
            _logger = _logChannelFactory.Get(nameof(AnalyticsController));
            InitServices();
        }

        private void InitServices()
        {
            foreach (var analyticsService in _injectedAnalyticsServices)
            {
                analyticsService.Init();

                if (analyticsService.IsInited)
                {
                    _analyticsServices[analyticsService.GetType()] = analyticsService;
                }
            }
        }

        public void SendEvent(IAnalyticsEvent analyticsEvent)
        {
            if (_analyticsServices.Count == 0)
            {
                _logger.LogError($"No active analytics service found! Event {analyticsEvent.Name} wasn't sent.");
                return;
            }

            if (analyticsEvent.Services.Count == 0)
            {
                SendToAll(analyticsEvent);
            }
            else
            {
                SendToCertain(analyticsEvent);
            }
        }

        private void SendToAll(IAnalyticsEvent analyticsEvent)
        {
            foreach (var analyticService in _analyticsServices.Values)
            {
                analyticService.SendEvent(analyticsEvent);
            }
                
            LogEvent(analyticsEvent, analyticsEvent.Services);
        }

        private void SendToCertain(IAnalyticsEvent analyticsEvent)
        {
            foreach (var analyticsServiceType in analyticsEvent.Services)
            {
                if (_analyticsServices.TryGetValue(analyticsServiceType, out var analyticService))
                {
                    analyticService.SendEvent(analyticsEvent);
                }
                else
                {
                    _logger.LogError($"Can't find '{analyticsServiceType.Name}' analytics service! Event '{analyticsEvent.Name}' wasn't sent.");
                }
            }
                
            LogEvent(analyticsEvent, analyticsEvent.Services);
        }

        private void LogEvent(IAnalyticsEvent analyticsEvent, IReadOnlyCollection<Type> analyticsServices)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("EventLogged.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Services:".SetSystemColor());

            if (analyticsServices.Count > 0)
            {
                foreach (var analyticService in analyticsServices)
                {
                    stringBuilder.AppendLine($"{analyticService.Name}");
                }
            }
            else
            {
                foreach (var analyticService in _analyticsServices)
                {
                    stringBuilder.AppendLine($"{analyticService.Key.Name}");
                }
            }
            
            stringBuilder.AppendLine();
            stringBuilder.Append(analyticsEvent.ToString());
            _logger.Log(stringBuilder.ToString());
        }
    }
}