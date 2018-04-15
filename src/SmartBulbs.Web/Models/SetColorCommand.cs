using LifxIoT.Api;
using LifxIoT.Models;
using Steeltoe.CircuitBreaker.Hystrix;
using System;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Models
{
    public class SetColorCommand : HystrixCommand<string>
    {
        private LifxApi _lifxClient;
        private string _newColor;
        private double _duration;

        public SetColorCommand(IHystrixCommandOptions options, string lifxKey, string newColor, double? duration = null) : 
            base(options)
        {
            _lifxClient = new LifxApi(lifxKey);
            _newColor = newColor;
            _duration = duration ?? 1;
        }

        protected override async Task<string> RunAsync()
        {
            try
            {
                await _lifxClient.SetState(new All(), new SentState { Color = $"#{_newColor}", Duration = _duration, Power = "on" });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            return _newColor;
        }

        protected override Task<string> RunFallbackAsync()
        {
            Console.WriteLine("Entered fallback method for SetColorCommand");
            return Task.FromResult(_newColor);
        }
    }
}
