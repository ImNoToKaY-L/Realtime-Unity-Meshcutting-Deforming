using UnityEngine;
using System.Collections;

public class MapChanger : MonoBehaviour {

	private int oldselected = -1;
	private Material targetmat;
	public Renderer tarRenderer;
	public Texture[] mattexes = new Texture[4];

	void Start () {
		if (!tarRenderer)
			return;

		targetmat = tarRenderer.material;
	}

	public void SwitchTexture (int toWhich) {
		if (!targetmat || mattexes.Length == 0 || toWhich < 0 || toWhich > mattexes.Length)
			return;

		if (toWhich != oldselected) {
			targetmat.SetTexture ("_BoobMap", mattexes [toWhich]);
			oldselected = toWhich;
		}

	}


}
