using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Routing;

namespace GithubActors.Actors
{
  /// <summary>
  /// Top-level actor responsible for coordinating and launching repo-processing jobs
  /// </summary>
  public class GithubCommanderActor : ReceiveActor, IWithUnboundedStash
  {
    #region Message classes

    public class CanAcceptJob
    {
      public CanAcceptJob(RepoKey repo)
      {
        Repo = repo;
      }

      public RepoKey Repo { get; private set; }
    }

    public class AbleToAcceptJob
    {
      public AbleToAcceptJob(RepoKey repo)
      {
        Repo = repo;
      }

      public RepoKey Repo { get; private set; }
    }

    public class UnableToAcceptJob
    {
      public UnableToAcceptJob(RepoKey repo)
      {
        Repo = repo;
      }

      public RepoKey Repo { get; private set; }
    }

    #endregion

    private IActorRef _coordinator;
    private IActorRef _canAcceptJobSender;
    public IStash Stash { get; set; }

    private int pendingJobReplies;

    public GithubCommanderActor()
    {
      Ready();
    }

    private void Ready()
    {
      Receive<CanAcceptJob>(job =>
      {
        _coordinator.Tell(job);
        BecomeAsking();
      });
    }

    private void BecomeAsking()
    {
      _canAcceptJobSender = Sender;
      var task = _coordinator.Ask<Routees>(new GetRoutees());
      var routees = task.Result;
      pendingJobReplies = routees.Members.Count();
      Become(Asking);
    }

    private void StashAll<T>()
    {
      Receive<T>(job => Stash.Stash());
    }

    private void Asking()
    {
      // stash any subsequent requests
      StashAll<CanAcceptJob>();

      Receive<UnableToAcceptJob>(unableToAcceptJob =>
      {
        pendingJobReplies--;
        if (pendingJobReplies == 0)
        {
          _canAcceptJobSender.Tell(unableToAcceptJob);
          BecomeReady();
        }
      });

      Receive<AbleToAcceptJob>(ableToAcceptJob =>
      {
        _canAcceptJobSender.Tell(ableToAcceptJob);

        // start processing messages
        Sender.Tell(new GithubCoordinatorActor.BeginJob(ableToAcceptJob.Repo));

        // launch the new window to view results of the processing
        Context.ActorSelection(ActorPaths.MainFormActor.Path).Tell(
          new MainFormActor.LaunchRepoResultsWindow(ableToAcceptJob.Repo, Sender));

        BecomeReady();
      });
    }

    private void BecomeReady()
    {
      Become(Ready);
      Stash.UnstashAll();
    }

    protected override void PreStart()
    {
      // create a broadcast router who will ask all if them if they're available for work
      _coordinator = Context
        .ActorOf(Props
          .Create(() => new GithubCoordinatorActor())
          .WithRouter(FromConfig.Instance),
          ActorPaths.GithubCoordinatorActor.Name);
      base.PreStart();
    }

    protected override void PreRestart(Exception reason, object message)
    {
      //kill off the old coordinator so we can recreate it from scratch
      _coordinator.Tell(PoisonPill.Instance);
      base.PreRestart(reason, message);
    }
  }
}
