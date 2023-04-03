using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public int factionNumber;
    public int tileX;
    public int tileZ;

    //Metqa defining play here
    public Queue<int> movementQueue;
    public Queue<int> combatQueue;

    public float visualMovementSpeed = .5f;

    public TileMap map;

    public GameObject tileBeingOccupied;

    //public List<Node> currentPath = null;

    [Header("Unit stats")]
    public int moveSpeed = 2;
    public int maxHealth = 10;
    public int currentHealth;
    public int attackDamage = 4;
    [Header("Abilities/Attacks")]
    public AbilityTypes[] abilityTypes;

    public Material availableMat;
    public Material waitMat;

    //Enum for unit states
    public enum movementStates
    {
        Unselected,
        Selected,
        Moved,
        Wait
    }
    public movementStates unitMoveState;

    //Pathfinding
    public List<Node> path = null;

    //Path for moving unit's transform
    public List<Node> pathForMovement = null;
    public bool completedMovement = false;

    private void Awake()
    {
        movementQueue= new Queue<int>();
        combatQueue = new Queue<int>();

        tileX = (int)transform.position.x;
        tileZ = (int)transform.position.z;
        unitMoveState = movementStates.Unselected;
        currentHealth = maxHealth;
    }

    public void MoveNextTile()
    {
        if (path.Count == 0)
        {
            return;
        }
        else
        {
            StartCoroutine(moveOverSeconds(transform.gameObject, path[path.Count - 1]));
        }
    }

    public void MoveAgain()
    {
        path = null;
        setMovementState(0);
        completedMovement = false;
        gameObject.GetComponentInChildren<Renderer>().material = availableMat;
    }

    public movementStates getMovementStateEnum(int state)
    {
        if (state == 0) { return movementStates.Unselected; }
        else if (state == 1) { return movementStates.Selected; }
        else if (state == 2) { return movementStates.Moved; }
        else if (state == 3) { return movementStates.Wait; }
        
        return movementStates.Unselected;
    }

    public void setMovementState(int stateNumb)
    {
        if (stateNumb == 0) { unitMoveState = movementStates.Unselected; }
        else if (stateNumb == 1) { unitMoveState = movementStates.Selected; }
        else if (stateNumb == 2) { unitMoveState = movementStates.Moved; }
        else if (stateNumb == 3) { unitMoveState = movementStates.Wait; }
    }

    public IEnumerator moveOverSeconds(GameObject objectToMove, Node endNode)
    {
        movementQueue.Enqueue(1);

        path.RemoveAt(0);
        while (path.Count != 0)
        {
            Vector3 endPos = map.TileCoordToWorldCoord(path[0].x, path[0].z);
            objectToMove.transform.position = Vector3.Lerp(transform.position, endPos, visualMovementSpeed);
            if ((transform.position - endPos).sqrMagnitude < 0.001)
            {
                path.RemoveAt(0);
                //moveSpeed--;
            }
            yield return new WaitForEndOfFrame();
        }
        visualMovementSpeed = 0.15f;
        transform.position = map.TileCoordToWorldCoord(endNode.x, endNode.z);

        tileX = endNode.x;
        tileZ = endNode.z;
        tileBeingOccupied.GetComponent<ClickableTile>().unitOnTile = null;
        tileBeingOccupied = map.tilesOnMap[tileX, tileZ];
        movementQueue.Dequeue();
    }

    public void Wait()
    {
        gameObject.GetComponentInChildren<Renderer>().material = waitMat;
    }

    public void TakeDamage(int damageTaken)
    {
        currentHealth = currentHealth - damageTaken;
    }

    public void UnitDeath()
    {
        Destroy(gameObject);
    }
}
