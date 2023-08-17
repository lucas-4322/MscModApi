﻿using System;
using System.Collections.Generic;
using System.Linq;
using MSCLoader;
using MscModApi.Caching;
using MscModApi.Parts.ReplacePart;
using MscModApi.Tools;
using MscModApi.Trigger;
using UnityEngine;
using static MscModApi.Parts.PartEvent;

namespace MscModApi.Parts
{
	public class Part : BasicPart, SupportsPartEvents, SupportsPartBehaviourEvents
	{
		protected static GameObject clampModel;
		protected int clampsAdded;
		internal PartSave partSave;
		protected Dictionary<Screw, int> preScrewPlacementModeEnableTightnessMap = new Dictionary<Screw, int>();
		private bool _screwPlacementMode;
		protected bool injectedScrewPlacementDisablePreUninstall;

		/// <summary>
		/// Stores all events that a developer may have added to this part object
		/// </summary>
		protected Dictionary<EventTime, Dictionary<PartEvent.EventType, List<Action>>> events =
			new Dictionary<EventTime, Dictionary<PartEvent.EventType, List<Action>>>();

		/// <inheritdoc />
		protected Part()
		{
		}

		public Part(string id, string name, GameObject part, Part parent, Vector3 installPosition,
			Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true) : this(id, name, part, (BasicPart) parent, installPosition, installRotation, partBaseInfo, uninstallWhenParentUninstalls, disableCollisionWhenInstalled)
		{
		}

		public Part(string id, string name, GameObject part, GamePart parent, Vector3 installPosition,
			Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true) : this(id, name, part, (BasicPart)parent, installPosition, installRotation, partBaseInfo, uninstallWhenParentUninstalls, disableCollisionWhenInstalled)
		{
		}

		protected Part(string id, string name, GameObject part, BasicPart parent, Vector3 installPosition,
			Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true)
		{
			gameObjectUsedForInstantiation = part;

			Setup(id, name, parent, installPosition, installRotation, partBaseInfo,
				uninstallWhenParentUninstalls, disableCollisionWhenInstalled, null);
		}

		public Part(string id, string name, PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null)
		{
			Setup(id, name, null, Vector3.zero, Vector3.zero, partBaseInfo,
				uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName);
		}

		public Part(string id, string name, Part parent, Vector3 installPosition, Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null) : this(id, name, (BasicPart) parent, installPosition, installRotation, partBaseInfo, uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName)
		{
		}

		public Part(string id, string name, GamePart parent, Vector3 installPosition, Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null) : this(id, name, (BasicPart) parent, installPosition, installRotation, partBaseInfo, uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName)
		{
		}

		protected Part(string id, string name, BasicPart parent, Vector3 installPosition, Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null)
		{
			Setup(id, name, parent, installPosition, installRotation, partBaseInfo,
				uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName);
		}

		public string id { get; protected set; }

		public PartBaseInfo partBaseInfo { get; protected set; }

		public override GameObject gameObject { get; protected set; }

		public Vector3 installPosition { get; protected set; }
		
		public Vector3 installRotation { get; protected set; }

		public BasicPart parent { get; protected set; }
		
		protected List<Screw> savedScrews;

		public Collider collider { get; protected set; }

		public TriggerWrapper trigger { get; protected set; }

		public Transform transform => gameObject.transform;

		public GameObject gameObjectUsedForInstantiation { get; protected set; }
		
		public bool hasParent => parent != null;

		public bool installBlocked { get; set; }

		public List<Screw> screws => partSave.screws;

		public override bool installed => partSave.installed;

		public bool screwPlacementMode
		{
			get => _screwPlacementMode;
			set
			{
				if (!installed)
				{
					return;
				}

				if (!injectedScrewPlacementDisablePreUninstall)
				{
					injectedScrewPlacementDisablePreUninstall = true;
					AddEventListener(EventTime.Pre, PartEvent.EventType.Uninstall, () => { screwPlacementMode = false; });
				}

				foreach (Screw screw in screws)
				{
					if (!value)
					{
						if (!preScrewPlacementModeEnableTightnessMap.TryGetValue(screw, out int preEnableTightness))
						{
							continue;
						}

						screw.tightness = Screw.maxTightness;
						screw.OutBy(Screw.maxTightness);
						screw.InBy(preEnableTightness);
						preScrewPlacementModeEnableTightnessMap.Remove(screw);
						continue;
					}

					if (preScrewPlacementModeEnableTightnessMap.ContainsKey(screw))
					{
						continue;
					}

					preScrewPlacementModeEnableTightnessMap.Add(screw, screw.tightness);
					screw.InBy(Screw.maxTightness);
					screw.tightness = 0;
				}

				if (!value && ScrewPlacementAssist.selectedPart == this)
				{
					ScrewPlacementAssist.HidePartInteraction();
				}

				_screwPlacementMode = value;
			}
		}

		/// <inheritdoc />
		public override string name => gameObject.name;

		/// <inheritdoc />
		public override bool isLookingAt => gameObject.IsLookingAt();

		/// <inheritdoc />
		public override bool isHolding => gameObject.IsHolding();

		public bool installPossible => !installBlocked && bought && trigger != null;

		/// <inheritdoc />
		public override bool bought
		{
			get => partSave.bought == PartSave.BoughtState.Yes || partSave.bought == PartSave.BoughtState.NotConfigured;
			set => partSave.bought = value ? PartSave.BoughtState.Yes : PartSave.BoughtState.No;
		}

		/// <inheritdoc />
		public override Vector3 position
		{
			get => gameObject.transform.position;
			set
			{
				if (!installed)
				{
					gameObject.transform.position = value;
				}
			}
		}

		/// <inheritdoc />
		public override Vector3 rotation
		{
			get => gameObject.transform.rotation.eulerAngles;
			set
			{
				if (!installed)
				{
					gameObject.transform.rotation = Quaternion.Euler(value);
				}
			}
		}

		/// <inheritdoc />
		public override bool active
		{
			get => gameObject.activeSelf;
			set => gameObject.SetActive(value);
		}

		public override bool bolted
		{
			get { return screws.Count > 0 && screws.All(screw => screw.tightness == Screw.maxTightness) && installed; }
		}

		public override bool installedOnCar => installed && gameObject.transform.root == CarH.satsuma.transform;

		protected void Setup(string id, string name, BasicPart parent, Vector3 installPosition,
			Vector3 installRotation, PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls,
			bool disableCollisionWhenInstalled, string prefabName)
		{
			InitEventStorage();
			this.id = id;
			this.partBaseInfo = partBaseInfo;
			this.installPosition = installPosition;
			this.uninstallWhenParentUninstalls = uninstallWhenParentUninstalls;
			this.installRotation = installRotation;

			if (gameObjectUsedForInstantiation != null) {
				gameObject = GameObject.Instantiate(gameObjectUsedForInstantiation);
				gameObject.SetNameLayerTag(name + "(Clone)");
			}
			else {
				gameObject = Helper.LoadPartAndSetName(partBaseInfo.assetBundle, prefabName ?? id, name);
			}

			if (!partBaseInfo.partsSave.TryGetValue(id, out partSave)) {
				partSave = new PartSave();
			}

			try {
				CustomSaveLoading(partBaseInfo.mod, $"{id}_saveFile.json");
			}
			catch {
				// ignored
			}

			savedScrews = new List<Screw>(partSave.screws);
			partSave.screws.Clear();

			collider = gameObject.GetComponent<Collider>();

			if (parent != null) {
				trigger = new TriggerWrapper(this, parent, disableCollisionWhenInstalled);
			}

			if (partSave.installed) {
				Install();
			}

			LoadPartPositionAndRotation(gameObject, partSave);

			if (!MscModApi.modSaveFileMapping.ContainsKey(partBaseInfo.mod.ID)) {
				MscModApi.modSaveFileMapping.Add(partBaseInfo.mod.ID, partBaseInfo.saveFilePath);
			}

			if (MscModApi.modsParts.TryGetValue(partBaseInfo.mod.ID, out var modParts)) {
				modParts.Add(id, this);
			}
			else {
				MscModApi.modsParts.Add(partBaseInfo.mod.ID, new Dictionary<string, Part>
				{
					{ id, this }
				});
			}

			partBaseInfo.AddToPartsList(this);
			this.parent = parent;
		}

		protected void InitEventStorage()
		{
			foreach (EventTime eventTime in Enum.GetValues(typeof(EventTime))) {
				Dictionary<PartEvent.EventType, List<Action>> eventTypeDict = new Dictionary<PartEvent.EventType, List<Action>>();

				foreach (PartEvent.EventType eventType in Enum.GetValues(typeof(PartEvent.EventType))) {
					eventTypeDict.Add(eventType, new List<Action>());
				}

				events.Add(eventTime, eventTypeDict);
			}
		}

		internal void ResetScrews()
		{
			foreach (var screw in partSave.screws) {
				screw.OutBy(screw.tightness);
			}
		}

		internal void SetScrewsActive(bool active)
		{
			partSave.screws.ForEach(delegate(Screw screw) { screw.gameObject.SetActive(active); });
		}

		public void Install()
		{
			trigger?.Install();
		}

		public override void Uninstall()
		{
			trigger?.Uninstall();
		}

		private void LoadPartPositionAndRotation(GameObject gameObject, PartSave partSave)
		{
			position = partSave.position;
			Quaternion tmpRotation = (partSave.rotation);
			rotation = tmpRotation.eulerAngles;
		}

		public void AddScrew(Screw screw)
		{
			screw.Verify();
			screw.SetPart(this);
			screw.parentCollider = gameObject.GetComponent<Collider>();
			partSave.screws.Add(screw);

			var index = partSave.screws.IndexOf(screw);

			screw.CreateScrewModel(index);

			screw.LoadTightness(savedScrews.ElementAtOrDefault(index));
			screw.InBy(screw.tightness, false, true);

			screw.gameObject.SetActive(installed);

			MscModApi.screws.Add(screw.gameObject.name, screw);
		}

		internal static void LoadAssets(AssetBundle assetBundle)
		{
			clampModel = assetBundle.LoadAsset<GameObject>("clamp.prefab");
		}

		public void AddScrews(Screw[] screws, float overrideScale = 0f, float overrideSize = 0f)
		{
			foreach (var screw in screws)
			{
				if (overrideScale != 0f)
				{
					screw.scale = overrideScale;
				}

				if (overrideSize != 0f)
				{
					screw.size = overrideSize;
				}

				AddScrew(screw);
			}
		}

		/// <inheritdoc />
		public T AddEventBehaviour<T>(PartEvent.EventType eventType) where T : Behaviour
		{
			var behaviour = AddComponent<T>();
			switch (eventType)
			{
				case PartEvent.EventType.Install:
					behaviour.enabled = installed;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.Uninstall, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.Uninstall:
					behaviour.enabled = !installed;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.Install, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.InstallOnCar:
					behaviour.enabled = installedOnCar;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.UninstallFromCar, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.UninstallFromCar:
					behaviour.enabled = !installedOnCar;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.InstallOnCar, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.Bolted:
					behaviour.enabled = bolted;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.Unbolted, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.Unbolted:
					behaviour.enabled = !bolted;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.Bolted, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.BoltedOnCar:
					behaviour.enabled = bolted && installedOnCar;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.UnboltedOnCar, () => behaviour.enabled = false);
					break;
				case PartEvent.EventType.UnboltedOnCar:
					behaviour.enabled = !bolted && installedOnCar;
					AddEventListener(EventTime.Post, eventType, () => behaviour.enabled = true);
					AddEventListener(EventTime.Post, PartEvent.EventType.BoltedOnCar, () => behaviour.enabled = false);
					break;
			}

			return behaviour;
		}

		public void AddEventListener(EventTime eventTime, PartEvent.EventType eventType, Action action)
		{
			if (
				eventTime == EventTime.Pre
				&& (eventType == PartEvent.EventType.InstallOnCar || eventType == PartEvent.EventType.UninstallFromCar)
			)
			{
				throw new Exception($"Event {eventType} can't be detected at '{eventTime}'. Unsupported!");
			}

			events[eventTime][eventType].Add(action);

			if (eventTime == EventTime.Post)
			{
				switch (eventType)
				{
					//ToDo: check if invoking just the newly added action is enough of if all have to be invoked
					case PartEvent.EventType.Install:
						if (installed)
						{
							action.Invoke();
						}

						break;
					case PartEvent.EventType.Uninstall:
						if (!installed)
						{
							action.Invoke();
						}

						break;
					case PartEvent.EventType.Bolted:
						if (bolted)
						{
							action.Invoke();
						}

						break;
					case PartEvent.EventType.Unbolted:
						if (!bolted)
						{
							action.Invoke();
						}
						break;
					case PartEvent.EventType.InstallOnCar:
						if (installedOnCar)
						{
							action.Invoke();
						}
						break;
					case PartEvent.EventType.UninstallFromCar:
						if (!installedOnCar)
						{
							action.Invoke();
						}
						break;
					case PartEvent.EventType.BoltedOnCar:
						if (bolted && installedOnCar)
						{
							action.Invoke();
						}
						break;
					case PartEvent.EventType.UnboltedOnCar:
						if (!bolted && installedOnCar)
						{
							action.Invoke();
						}
						break;
				}
			}
		}

		public List<Action> GetEvents(EventTime eventTime, PartEvent.EventType eventType)
		{
			return events[eventTime][eventType];
		}

		public T AddComponent<T>() where T : Component => gameObject.AddComponent(typeof(T)) as T;

		public T GetComponent<T>() => gameObject.GetComponent<T>();

		/// <inheritdoc />
		public override void ResetToDefault(bool uninstall = false)
		{
			if (uninstall && installed)
			{
				Uninstall();
			}

			position = defaultPosition;
			rotation = defaultRotation;
		}

		public void AddClampModel(Vector3 position, Vector3 rotation, Vector3 scale)
		{
			var clamp = GameObject.Instantiate(clampModel);
			clamp.name = $"{gameObject.name}_clamp_{clampsAdded}";
			clampsAdded++;
			clamp.transform.SetParent(gameObject.transform);
			clamp.transform.localPosition = position;
			clamp.transform.localScale = scale;
			clamp.transform.localRotation = new Quaternion { eulerAngles = rotation };
		}
		

		[Obsolete("Use 'AddEventBehaviour' method instead", true)]
		public T AddWhenInstalledBehaviour<T>() where T : Behaviour
		{
			return AddEventBehaviour<T>(PartEvent.EventType.Install);
		}

		[Obsolete("Use 'AddEventBehaviour' method instead", true)]
		public T AddWhenUninstalledBehaviour<T>() where T : Behaviour
		{
			return AddEventBehaviour<T>(PartEvent.EventType.Uninstall);
		}

		[Obsolete("Use 'screws' property instead", true)]
		public List<Screw> GetScrews()
		{
			return screws;
		}


		[Obsolete("Use 'installed' property instead", true)]
		public bool IsInstalled()
		{
			return installed;
		}

		[Obsolete("Use 'bolted' property instead", true)]
		public bool IsFixed(bool ignoreUnsetScrews = true)
		{
			return bolted;
		}

		[Obsolete("Use 'parentInstalled' property instead", true)]
		internal bool ParentInstalled()
		{
			return parent.installed;
		}

		[Obsolete("Use 'parentBolted' property instead", true)]
		public bool ParentFixed()
		{
			return parent.bolted;
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPreSaveAction(Action action)
		{
			AddEventListener(EventTime.Pre, PartEvent.EventType.Save, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPreInstallAction(Action action)
		{
			AddEventListener(EventTime.Pre, PartEvent.EventType.Install, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPostInstallAction(Action action)
		{
			AddEventListener(EventTime.Post, PartEvent.EventType.Install, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPreUninstallAction(Action action)
		{
			AddEventListener(EventTime.Pre, PartEvent.EventType.Uninstall, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPostUninstallAction(Action action)
		{
			AddEventListener(EventTime.Post, PartEvent.EventType.Uninstall, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPostFixedAction(Action action)
		{
			AddEventListener(EventTime.Post, PartEvent.EventType.Bolted, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPreFixedAction(Action action)
		{
			AddEventListener(EventTime.Pre, PartEvent.EventType.Bolted, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPreUnfixedActions(Action action)
		{
			AddEventListener(EventTime.Pre, PartEvent.EventType.Unbolted, action);
		}

		[Obsolete("Use cleaner 'AddEventListener' method instead", true)]
		public void AddPostUnfixedActions(Action action)
		{
			AddEventListener(EventTime.Post, PartEvent.EventType.Unbolted, action);
		}

		[Obsolete("Use AddWhenInstalledBehaviour instead. Will be removed in a later version", true)]
		public T AddWhenInstalledMono<T>() where T : MonoBehaviour
		{
			return AddWhenInstalledBehaviour<T>();
		}

		[Obsolete("Use AddWhenUninstalledBehaviour instead. Will be removed in a later version", true)]
		public T AddWhenUninstalledMono<T>() where T : MonoBehaviour
		{
			return AddWhenUninstalledBehaviour<T>();
		}

		[Obsolete("Use 'installBlocked' property instead", true)]
		public void BlockInstall(bool block)
		{
			installBlocked = block;
		}

		[Obsolete("Use 'installBlocked' property instead", true)]
		public bool IsInstallBlocked()
		{
			return installBlocked;
		}

		[Obsolete("Use 'hasParent' property instead", true)]
		public bool HasParent()
		{
			return hasParent;
		}

		[Obsolete("Use 'screwPlacementMode' property instead", true)]
		public void EnableScrewPlacementMode()
		{
			screwPlacementMode = true;
		}

		[Obsolete("Use 'screwPlacementMode' property instead", true)]
		internal bool IsInScrewPlacementMode()
		{
			return screwPlacementMode;
		}

		public virtual void CustomSaveLoading(Mod mod, string saveFileName)
		{
			throw new Exception("Only subclasses should not throw an error");
		}

		public virtual void CustomSaveSaving(Mod mod, string saveFileName)
		{
			throw new Exception("Only subclasses should not throw an error");
		}
	}
}