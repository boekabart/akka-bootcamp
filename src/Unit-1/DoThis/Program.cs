using Akka.Actor;

namespace WinTail
{
  #region Program
  class Program
  {
    public static ActorSystem MyActorSystem;

    static void Main(string[] args)
    {
      // initialize MyActorSystem
      MyActorSystem = ActorSystem.Create("MyActorSystem");

      var consoleWriterProps = Props.Create<ConsoleWriterActor>();
      var consoleWriterActor = MyActorSystem.ActorOf(consoleWriterProps, "consoleWriterActor");

      Props tailCoordinatorProps = Props.Create(() => new TailCoordinatorActor());
      MyActorSystem.ActorOf(tailCoordinatorProps, "tailCoordinatorActor");

      var validationActorProps = Props.Create(() => new FileValidatorActor(consoleWriterActor));
      MyActorSystem.ActorOf(validationActorProps, "validationActor");
      var consoleReaderProps = Props.Create<ConsoleReaderActor>();
      var consoleReaderActor = MyActorSystem.ActorOf(consoleReaderProps, "consoleReaderActor");

      // tell console reader to begin
      consoleReaderActor.Tell(ConsoleReaderActor.StartCommand);

      // blocks the main thread from exiting until the actor system is shut down
      MyActorSystem.AwaitTermination();
    }
  }
  #endregion
}
