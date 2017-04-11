using UnityEngine;
using System.Collections;

public class TestHud : MonoBehaviour {

	void OnEmojiHrefClicked(string href)
	{
		Debug.Log("TestHud.OnEmojiHrefClicked " + href);
	}

	void OnThisButtonClicked()
	{
		Debug.Log("OnThisButtonClicked");
    }
}
