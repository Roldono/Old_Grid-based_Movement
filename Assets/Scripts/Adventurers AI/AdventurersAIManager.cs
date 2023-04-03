using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

public class AdventurersAIManager : MonoBehaviour
{
    [Header("Manager scripts")]
    public TileMap TM;
    public GameManagerScript GMS;

    [Header("Adventurer units")]
    public GameObject adventurerUnits;
    public List<GameObject> adventurersUnitsLeft = new List<GameObject>();

    public GameObject monsterUnits;
    public List<GameObject> monsterUnitsLeft = new List<GameObject>();

    public List<Node> routeToMonsterUnit = null;
    public List<Node> tempRouteToMonsterUnit = null;

    public float remainingMovement;
    public int countdownRemainingMovement;

    bool turnStarted = false;
    bool unitBeingMoved = false;
    bool waitingBetweenActions = false;
    bool routeChecked = false;
    int unitNumber = 0;

    private void Start()
    {
        TM = GetComponent<TileMap>();
        GMS = GetComponent<GameManagerScript>();

        routeToMonsterUnit = new List<Node>();
        tempRouteToMonsterUnit = new List<Node>();
    }

    private void Update()
    {
        if (!waitingBetweenActions && turnStarted && unitNumber < adventurersUnitsLeft.Count)
        {
            if (!routeChecked)
            {
                TM.AdventurerUnitToSelect(unitNumber);
                CheckAIUnitRoute();
                StartCoroutine(WaitBetweenActions());
            }
            else if (!unitBeingMoved)
            {
                MoveAdventurer();
            }
            else if (unitBeingMoved)
            {
                if (TM.selectedUnit.GetComponent<Unit>().path.Count == 0)
                {
                    TM.FinalizeAIOption();
                    unitBeingMoved = false;
                    routeChecked = false;
                    unitNumber++;
                    StartCoroutine(WaitBetweenActions());
                }
            }
        }
        else if (!waitingBetweenActions && turnStarted && !unitBeingMoved && unitNumber == adventurersUnitsLeft.Count)
        {
            turnStarted = false;
            GMS.EndTurn();
        }
    }

    //IN:
    //OUT: void
    //DESC: Starts the different actions that the AI has to take during its turn
    public void AdventurersTurn()
    {
        UnitsLeft();
        unitNumber = 0;
        turnStarted = true;
    }

    public void MoveAdventurer()
    {
        unitBeingMoved = true;
        if (routeToMonsterUnit.Count > remainingMovement)
        {
            for (int i = routeToMonsterUnit.Count; i > remainingMovement + 1; i--)
            {
                routeToMonsterUnit.RemoveAt(i - 1);
            }
        }
        TM.selectedUnit.GetComponent<Unit>().path = routeToMonsterUnit;

        TM.moveUnit();
        TM.MoveUnitAndFinalize();
        //StartCoroutine(MoveAIUnit());
    }

    //IN:
    //OUT: void
    //DESC: Check what units are left for both factions and add these units to their respective lists
    void UnitsLeft()
    {
        adventurersUnitsLeft.Clear();
        foreach (Transform adventurer in adventurerUnits.transform)
        {
            adventurersUnitsLeft.Add(adventurer.gameObject);
        }
        monsterUnitsLeft.Clear();
        foreach (Transform monster in monsterUnits.transform)
        {
            monsterUnitsLeft.Add(monster.gameObject);
        }
    }

    //IN:
    //OUT: void
    //DESC: Moves the selected AI unit as far as it's possible in the direction of the closest monster unit
    void CheckAIUnitRoute()
    {
        routeToMonsterUnit = null;
        foreach (GameObject monster in monsterUnitsLeft)
        {
            tempRouteToMonsterUnit = GenerateRouteToMonsterUnit(monster.GetComponent<Unit>().tileX, monster.GetComponent<Unit>().tileZ);
            if (routeToMonsterUnit == null || routeToMonsterUnit.Count > tempRouteToMonsterUnit.Count)
            {
                routeToMonsterUnit = new List<Node>();
                routeToMonsterUnit = tempRouteToMonsterUnit;
            }
            Debug.Log(routeToMonsterUnit.Count);
        }
        remainingMovement = TM.selectedUnit.GetComponent<Unit>().moveSpeed;
        if (remainingMovement > 0)
        {
            int stepsTaken = 0;
            bool allStepsTaken = false;
            if (remainingMovement >= routeToMonsterUnit.Count)
            {
                remainingMovement = routeToMonsterUnit.Count - 1;
            }
            while (!allStepsTaken)
            {
                if (stepsTaken <= remainingMovement)
                {
                    int check = (int)remainingMovement - stepsTaken;
                    int nodeX = routeToMonsterUnit[check].x;
                    int nodeZ = routeToMonsterUnit[check].z;

                    if (stepsTaken == 0)
                    {
                        GMS.SetCorrectRouteFinalTile(nodeX, nodeZ, stepsTaken, false);
                    }
                    else
                    {
                        //This is used to set the indicator for tiles excluding the first/last tile
                        GMS.setCorrectRouteWithInputAndOutout(nodeX, nodeZ, stepsTaken, false);
                    }
                    TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ].GetComponent<Renderer>().enabled = true;
                }
                stepsTaken++;
                if (stepsTaken == remainingMovement)
                {
                    allStepsTaken = true;
                }
            }
        }
        routeChecked = true;
    }

    //IN:
    //OUT: IEnumerator
    //DESC: Makes the rest of update wait between each action
    IEnumerator WaitBetweenActions()
    {
        waitingBetweenActions = true;
        yield return new WaitForSeconds(1);
        waitingBetweenActions = false;
    }

    //IN: x and z locations of monster unit
    //OUT: List of nodes to travers
    //DESC: Generate the route from the current selected AI unit to a monster unit
    public List<Node> GenerateRouteToMonsterUnit(int x, int z)
    {
        if (TM.selectedUnit.GetComponent<Unit>().tileX == x - 1 && TM.selectedUnit.GetComponent<Unit>().tileZ == z ||
            TM.selectedUnit.GetComponent<Unit>().tileX == x + 1 && TM.selectedUnit.GetComponent<Unit>().tileZ == z ||
            TM.selectedUnit.GetComponent<Unit>().tileX == x && TM.selectedUnit.GetComponent<Unit>().tileZ == z - 1 ||
            TM.selectedUnit.GetComponent<Unit>().tileX == x && TM.selectedUnit.GetComponent<Unit>().tileZ == z + 1)
        {
            //The selected unit is already next to the target no need to find a route
            tempRouteToMonsterUnit = new List<Node>();
            return null;
        }
        if (TM.UnitCanEnterTile(x, z) == false)
        {
            //Can't move into something so we can probably just return
            //Can't set this endpoint as our goal
            return null;
        }

        tempRouteToMonsterUnit = null;

        //Dijkstra's method
        Dictionary<Node, float> dist = new Dictionary<Node, float>();
        Dictionary<Node, Node> prev = new Dictionary<Node, Node>();
        Node source = TM.graph[TM.selectedUnit.GetComponent<Unit>().tileX, TM.selectedUnit.GetComponent<Unit>().tileZ];
        Node target = TM.graph[x, z];
        dist[source] = 0;
        prev[source] = null;
        //Unchecked Nodes
        List<Node> unvisited = new List<Node>();

        //Initialize
        foreach (Node n in TM.graph)
        {
            //Initialize to infinite distance as we don't know the answer
            //Also some places are infinity
            if (n != source)
            {
                dist[n] = Mathf.Infinity;
                prev[n] = null;
            }
            unvisited.Add(n);
        }

        //if there is a node in the unvisited list let's check it
        while (unvisited.Count > 0)
        {
            //u will be the unvisited node with the shortest distance
            Node u = null;
            foreach (Node possibleU in unvisited)
            {
                if (u == null || dist[possibleU] < dist[u])
                {
                    u = possibleU;
                }
            }

            if (u == target)
            {
                break;
            }

            unvisited.Remove(u);
            foreach (Node n in u.neighbours)
            {
                float alt = dist[u] + TM.CostToEnterTile(n.x, n.z);
                if (alt < dist[n])
                {
                    dist[n] = alt;
                    prev[n] = u;
                }
            }
        }
        
        //If we're here we found the shortest path, or no path exists
        if (prev[target] == null)
        {
            //No route
            return null;
        }
        tempRouteToMonsterUnit = new List<Node>();
        Node curr = target;
        //Step through the current path and add it to the chain
        while (curr != null)
        {
            tempRouteToMonsterUnit.Add(curr);
            curr = prev[curr];
        }
        //We can't end on the same tile as the monster so we're removing its tile from the list
        tempRouteToMonsterUnit.RemoveAt(0);
        //Now routeToMonsterUnit is from target to source, we need to reverse it from source to target
        tempRouteToMonsterUnit.Reverse();
        return tempRouteToMonsterUnit;
    }
}
