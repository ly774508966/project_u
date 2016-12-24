using UnityEngine;
using System.Collections;

public class TestStub : MonoBehaviour {

	// Use this for initialization
	void Start () {
		var btn = GetComponent<UnityEngine.UI.Button>();
		btn.onClick.AddListener(
			() => Debug.Log("Clicked From Csharp"));
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
