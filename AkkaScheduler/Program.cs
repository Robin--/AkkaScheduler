using System;
using System.Threading;
using Akka.Actor;
using Mono.Unix;
using Mono.Unix.Native;
using NLog;

namespace AkkaScheduler
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var app = new SchedulerApp();
            app.Init();

            for (var i = 0; i < 2; i++)
            {
                app.StartSheduler();
                Thread.Sleep(30 * 1000);
                app.StopScheduler();
                Thread.Sleep(5000);
            }

            app.Terminate();

            if (Type.GetType("Mono.Runtime") != null)
            {
                UnixSignal.WaitAny(new[]
                    {
                            new UnixSignal(Signum.SIGINT),
                            new UnixSignal(Signum.SIGTERM),
                            new UnixSignal(Signum.SIGQUIT),
                            new UnixSignal(Signum.SIGHUP)
                        });
            }
            else
            {
                Console.ReadKey(true);
            }
        }
    }

    internal class SchedulerApp
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private IActorRef _scheduler;

        public void Init()
        {
            _scheduler = SchedulerSystem.System
                .ActorOf<Scheduler>("scheduler");
        }

        public void StartSheduler()
        {
            _scheduler.Tell("start");
        }

        public void StopScheduler()
        {
            _scheduler.Tell("stop");
        }

        public void Terminate()
        {
            SchedulerSystem.System.Terminate()
                .ContinueWith(task =>
                {
                    Logger.Info("System was terminated.");
                });
        }
    }

    internal class SchedulerSystem
    {
        private static ActorSystem _system;
        public static ActorSystem System => _system ?? (_system = ActorSystem.Create("SchedulerSystem"));
    }

    internal class Scheduler : ReceiveActor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private ICancelable _cancelable;

        public Scheduler()
        {
            Become(Wait);
        }

        private void Wait()
        {
            Receive<string>(msg =>
            {
                if (msg == "start")
                {
                    Become(Working);
                    Logger.Info("Scheduler started.");
                }
            });
        }

        private void Working()
        {
            _cancelable = Context.System.Scheduler
                .ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), Self, "Hello World!", Self);

            Receive<string>(msg =>
            {
                if (msg == "stop")
                {
                    _cancelable.Cancel();
                    Logger.Info("Scheduler stoped.");
                    Become(Wait);
                }
                else
                {
                    Logger.Trace(msg);
                }
            });
        }
    }
}
