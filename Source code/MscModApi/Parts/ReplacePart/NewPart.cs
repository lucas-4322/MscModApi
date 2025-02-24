﻿using System;
using HutongGames.PlayMaker;
using MSCLoader;
using System.Linq;
using MscModApi.Tools;
using UnityEngine;

namespace MscModApi.Parts.ReplacePart
{
	[Obsolete(
		"Soon to be made obsolete, will be replaced with a new implementation using the new 'GamePart' wrapper class")]
	public class NewPart : BasicPart
	{
		public Part part;

		public NewPart(Part part, bool canBeInstalledWithoutReplacing = false)
		{
			this.part = part;
			this.canBeInstalledWithoutReplacing = canBeInstalledWithoutReplacing;
		}

		public override GameObject gameObject
		{
			get => part.gameObject;
			protected set { }
		}

		/// <inheritdoc />
		public override bool isLookingAt => part.isLookingAt;

		/// <inheritdoc />
		public override bool isHolding => part.isHolding;

		/// <inheritdoc />
		public override string name => part.name;

		public override bool installed => part.installed;

		public bool canBeInstalledWithoutReplacing { get; protected set; }

		public override bool bought
		{
			get => part.bought;
			set => part.bought = value;
		}

		public override Vector3 position
		{
			get => part.position;
			set => part.position = value;
		}

		public override Vector3 rotation
		{
			get => part.rotation;
			set => part.rotation = value;
		}

		public override bool active
		{
			get => part.active;
			set => part.active = value;
		}

		public override void Uninstall()
		{
			part.Uninstall();
		}

		public override void ResetToDefault(bool uninstall = false)
		{
			part.ResetToDefault(uninstall);
		}

		public override bool installBlocked
		{
			get => part.installBlocked;
			set
			{
				if (!canBeInstalledWithoutReplacing) {
					part.installBlocked = value;
				}
			}
		}

		public override bool bolted => part.bolted;
		public override bool hasBolts => part.hasBolts;

		/// <inheritdoc />
		public override bool installedOnCar => part.installedOnCar;
	}
}