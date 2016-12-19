using UnityEngine;
using System.Collections;

namespace comext.utils
{
	public delegate void OnAppUpdate();
	public delegate void OnAppLateUpdate();
	public delegate void OnAppPause(bool pauseStatus);
	
	internal class AppController : MonoBehaviour
	{
		public event OnAppUpdate onAppUpdate;
		public event OnAppLateUpdate onAppLateUpdate;
		public event OnAppPause onAppPause;

		void Update()
		{
			if (onAppUpdate != null)
			{
				onAppUpdate();
			}
		}

		void LateUpdate()
		{
			if (onAppLateUpdate != null)
			{
				onAppLateUpdate();
			}
		}

		void OnApplicationPause(bool pauseStatus)
		{
			if (onAppPause != null)
			{
				onAppPause(pauseStatus);
			}
		}
	}

}