using UnityEngine;
using System.Collections;
using System;

namespace comext.utils
{
	public class App
	{
		class CoroutineExecutor : MonoBehaviour { }
		static CoroutineExecutor appBehaviour;
		static CoroutineExecutor unbreakableAppBehaviour;
		static AppController appController;

		static int mainThreadId;

		public static void Init()
		{
			mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

			var go = new GameObject("_App");
			appBehaviour = go.AddComponent<CoroutineExecutor>();
			unbreakableAppBehaviour = go.AddComponent<CoroutineExecutor>();
			appController = go.AddComponent<AppController>();
			appController.onAppUpdate += Update;

			GameObject.DontDestroyOnLoad(go);
		}

		public static event OnAppUpdate onAppUpate
		{
			add
			{
				AssertMainThread();
				appController.onAppUpdate += value;
			}
			remove
			{
				AssertMainThread();
				appController.onAppUpdate -= value;
			}
		}

		public static event OnAppLateUpdate onAppLateUpdate
		{
			add
			{
				AssertMainThread();
				appController.onAppLateUpdate += value;
			}
			remove
			{
				AssertMainThread();
				appController.onAppLateUpdate -= value;
			}
		}

		public static event OnAppPause onAppPause
		{
			add
			{
				AssertMainThread();
				appController.onAppPause += value;
			}
			remove
			{
				AssertMainThread();
				appController.onAppPause -= value;
			}
		}

		public static void AssertMainThread()
		{
			Debug.Assert(mainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId);
		}

		public static void StartCoroutine(IEnumerator routine, MonoBehaviour behaviour = null)
		{
			AssertMainThread();
			if (behaviour == null) behaviour = appBehaviour;
			behaviour.StartCoroutine(routine);
		}

		public static void StopAllCoroutines()
		{
			AssertMainThread();
			appBehaviour.StopAllCoroutines();
		}

		public static void StartCoroutineUnbreakable(IEnumerator routine)
		{
			AssertMainThread();
			unbreakableAppBehaviour.StartCoroutine(routine);
		}

		const int kDefaultTaskSize = 32;
		const float kDefaultTaskSizeGrowFactor = 1.5f;
		const float kTaskExecutionTimeOnMainThread = 0.2f;

		static Queue taskQueue = new Queue(kDefaultTaskSize, kDefaultTaskSizeGrowFactor);
		struct Task
		{
			public System.Threading.WaitCallback task;
			public object state;
		}

		public static void PerformTaskOnMainThread(System.Threading.WaitCallback task, object state = null)
		{
			var t = new Task();
			t.task = task;
			t.state = state;
			lock (taskQueue)
			{
				taskQueue.Enqueue(t);
			}
		}

		public static void PerformAsyncTask(System.Threading.WaitCallback task, object state = null, System.Threading.WaitCallback completeOnMainThread = null)
		{
			System.Threading.ThreadPool.QueueUserWorkItem(
				(state_) => 
				{
					task(state_);
					if (completeOnMainThread != null)
					{
						PerformTaskOnMainThread(completeOnMainThread, state_);
					}
				},
				state);
		}

		static void Update()
		{
			AssertMainThread();
			lock (taskQueue)
			{
				var timeUp = Time.realtimeSinceStartup + kTaskExecutionTimeOnMainThread;
				while (taskQueue.Count > 0 && Time.realtimeSinceStartup < timeUp)
				{
					var t = (Task)taskQueue.Dequeue();
					t.task(t.state);
				}
			}
		}







		
	}
}
