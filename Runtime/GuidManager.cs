using System;
using System.Collections.Generic;
using UnityEngine;

// Class to handle registering and accessing objects by GUID
namespace AeLa.Utilities.GUID
{
	public class GuidManager
	{
		// for each GUID we need to know the Game Object it references
		// and an event to store all the callbacks that need to know when it is destroyed
		private struct GuidInfo
		{
			public GameObject go;

			public event Action<GameObject> OnAdd;
			public event Action OnRemove;

			// todo: does GetInvocationList generate garbage?
			public bool HasListeners => OnAdd?.GetInvocationList().Length > 0 ||
			                            OnRemove?.GetInvocationList().Length > 0;

			public GuidInfo(GuidComponent comp)
			{
				go = comp.gameObject;
				OnRemove = null;
				OnAdd = null;
			}

			public void HandleAddCallback()
			{
				if (OnAdd != null)
				{
					OnAdd(go);
				}
			}

			public void HandleRemoveCallback()
			{
				if (OnRemove != null)
				{
					OnRemove();
				}
			}
		}

		// Singleton interface
		static GuidManager Instance;

		// All the public API is static so you need not worry about creating an instance
		public static bool Add(GuidComponent guidComponent)
		{
			Instance ??= new();

			return Instance.InternalAdd(guidComponent);
		}

		public static void Remove(Guid guid)
		{
			Instance ??= new();

			Instance.InternalRemove(guid);
		}

		public static GameObject ResolveGuid(Guid guid, Action<GameObject> onAddCallback, Action onRemoveCallback)
		{
			Instance ??= new();

			return Instance.ResolveGuidInternal(guid, onAddCallback, onRemoveCallback);
		}

		public static void RemoveCallbacks(Guid guid, Action<GameObject> onAddCallback, Action onRemoveCallback)
		{
			Instance ??= new();
			Instance.RemoveCallbacksInternal(guid, onAddCallback, onRemoveCallback);
		}

		public void RemoveCallbacksInternal(Guid guid, Action<GameObject> onAddCallback, Action onRemoveCallback)
		{
			if (!guidToObjectMap.TryGetValue(guid, out var info)) return;

			if (onAddCallback != null) info.OnAdd -= onAddCallback;
			if (onRemoveCallback != null) info.OnRemove -= onRemoveCallback;

			guidToObjectMap[guid] = info;
		}

		public static GameObject ResolveGuid(Guid guid, Action onDestroyCallback)
		{
			Instance ??= new();

			return Instance.ResolveGuidInternal(guid, null, onDestroyCallback);
		}

		public static GameObject ResolveGuid(Guid guid)
		{
			Instance ??= new();

			return Instance.ResolveGuidInternal(guid, null, null);
		}

		// instance data
		private Dictionary<Guid, GuidInfo> guidToObjectMap;

		private GuidManager()
		{
			guidToObjectMap = new();
		}

		private bool InternalAdd(GuidComponent guidComponent)
		{
			Guid guid = guidComponent.GetGuid();

			if (!guidToObjectMap.TryGetValue(guid, out var info))
			{
				guidToObjectMap.Add(guid, info = new(guidComponent));
				return true;
			}

			if (info.go != null && info.go != guidComponent.gameObject)
			{
				// normally, a duplicate GUID is a big problem, means you won't necessarily be referencing what you expect
				if (Application.isPlaying)
				{
					Debug.AssertFormat(
						false, guidComponent,
						"Guid Collision Detected between {0} and {1}.\nAssigning new Guid. Consider tracking runtime instances using a direct reference or other method.",
						(guidToObjectMap[guid].go != null ? guidToObjectMap[guid].go.name : "NULL"),
						(guidComponent != null ? guidComponent.name : "NULL")
					);
				}
				else
				{
					// however, at editor time, copying an object with a GUID will duplicate the GUID resulting in a collision and repair.
					// we warn about this just for pedantry reasons, and so you can detect if you are unexpectedly copying these components
					Debug.LogWarningFormat(
						guidComponent, "Guid Collision Detected while creating {0}.\nAssigning new Guid.",
						(guidComponent != null ? guidComponent.name : "NULL")
					);
				}

				return false;
			}

			// if we already tried to find this GUID, but haven't set the game object to anything specific, copy any OnAdd callbacks then call them
			info.go = guidComponent.gameObject;
			info.HandleAddCallback();
			guidToObjectMap[guid] = info;
			return true;
		}

		private void InternalRemove(Guid guid)
		{
			if (!guidToObjectMap.TryGetValue(guid, out var info)) return;

			// trigger all the destroy delegates that have registered
			info.HandleRemoveCallback();
			info.go = null;

			// info isn't storing callbacks and can safely be removed
			if (!info.HasListeners)
			{
				guidToObjectMap.Remove(guid);
			}
			else
			{
				guidToObjectMap[guid] = info;
			}
		}

		// nice easy api to find a GUID, and if it works, register an on destroy callback
		// this should be used to register functions to cleanup any data you cache on finding
		// your target. Otherwise, you might keep components in memory by referencing them
		private GameObject ResolveGuidInternal(Guid guid, Action<GameObject> onAddCallback, Action onRemoveCallback)
		{
			if (guidToObjectMap.TryGetValue(guid, out var info))
			{
				if (onAddCallback != null)
				{
					info.OnAdd += onAddCallback;
				}

				if (onRemoveCallback != null)
				{
					info.OnRemove += onRemoveCallback;
				}

				guidToObjectMap[guid] = info;
				return info.go;
			}

			info = new();
			if (onAddCallback != null)
			{
				info.OnAdd += onAddCallback;
			}

			if (onRemoveCallback != null)
			{
				info.OnRemove += onRemoveCallback;
			}

			guidToObjectMap.Add(guid, info);

			return null;
		}
	}
}