using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickableTile : MonoBehaviour
{
    public int tileX;
    public int tileZ;
    public TileMap map;
    public GameObject unitOnTile;

    private void OnMouseUp()
    {
        //Debug.Log("Click");
    }

    private Color startcolor;
}