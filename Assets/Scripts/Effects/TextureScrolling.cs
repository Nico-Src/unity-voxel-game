using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureScrolling : MonoBehaviour
{
    // scroll multipliers
    private float scrollX = 0.15f;
    private float scrollY = 0.15f;

    void Update()
    {
        // update scroll position every frame
        float updatedScrollX = Time.time * scrollX;
        float updatedScrollY = Time.time * scrollY;

        GetComponent<Renderer>().material.mainTextureOffset = new Vector2(updatedScrollX, updatedScrollY);
    }
}
