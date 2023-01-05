using System.Collections.Concurrent;

using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.SessionManagement;

public class UnitySessionService : IUnitySessionService
{
	private readonly ISessionFactory _sessionFactory;
	private readonly ILogger<UnitySessionService> _logger;

	private readonly ConcurrentBag<ApplicationRentTracker> _sessions = new();

	public UnitySessionService(ISessionFactory sessionFactory, ILogger<UnitySessionService> logger)
	{
		_sessionFactory = sessionFactory;
		_logger = logger;
	}

	public IRentedSession RentConnection()
	{
		var application = RetrieveApplication();

		_logger.LogInformation("OnBase session '{0}' rented", application.SessionID);

		return new RentedSession(this, application);
	}

	internal void Release(IRentedSession session)
	{
		_sessions.Add(new ApplicationRentTracker(session.UnityApplication, DateTime.Now));

		_logger.LogInformation("OnBase session '{0}' released", session.UnityApplication.SessionID);
	}

	private Application RetrieveApplication()
	{
		/*
		 * if there is a session in our list, and we can take it out of the list (concurrency conflict prevents this)
		 * then check to see if we can connect to the server. If we cannot connect to the server then call this method
		 * recursively until we can, or the list is empty
		 *
		 * if the list is empty, then create a brand new application that can be used
		 */
		if (_sessions.Count != 0 && _sessions.TryTake(out var session))
		{
			// if we've passed a long period of inactivity then it's time to disconnect
			if (session.LastRented.AddHours(1) < DateTime.Now)
			{
				_logger.LogInformation(
					"OnBase Session '{0}' evicted due to inactivity",
					session.Application.SessionID);
				
				session.Application.Dispose();
				return RetrieveApplication();
			}

			if (session.Application.IsSessionConnected())
				return session.Application;

			// this session was unable to connect, so dispose and move on
			_logger.LogInformation(
				"OnBase Session '{0}' indicated it was unable to connect to unity. Disposing connection and retrieving next",
				session.Application.SessionID);
			session.Application.Dispose();

			return RetrieveApplication();
		}

		return _sessionFactory.CreateApplication();
	}

	record ApplicationRentTracker(Application Application, DateTime LastRented);
}