#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ThirdParty{
	public class OcclusionBase : MonoBehaviour {
		public bool isVisible { get; set; }
		public DateTime lastSeen { get; set; }
	}
}