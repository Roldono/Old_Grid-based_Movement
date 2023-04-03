using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro.EditorUtilities;
using UnityEngine;
using UnityEngine.Experimental.AI;

public class GameManagerScript : MonoBehaviour
{
    //Raycast for the update for unitHove info
    private Ray ray;
    private RaycastHit hit;

    public int numberOfFactions = 2;
    public int currentFaction;
    public GameObject unitsOnBoard;

    public GameObject factionMonsters;
    public GameObject factionAdventurers;

    public GameObject unitBeingDisplayed;
    public GameObject tileBeingDisplayed;
    public bool displayingUnitInfo;


    public TileMap TM;
    public AdventurersAIManager AIM;

    //Cursor info for TileMap script
    public int cursorX;
    public int cursorZ;
    //currentTileBeingMousedOver
    public int selectedXTile;
    public int selectedZTile;

    //Variables for unitPotentialMovementRoute
    List<Node> currentPathForUnitRoute;
    List<Node> unitPathToCursor;

    public bool unitPathExists;

    //Player UI Materials
    public Material UIunitRoute;
    public Material UIunitRouteCurve;
    public Material UIunitRouteArrow;
    public Material UICursor;


    //AI UI Materials
    public Material adventurerUIUnitRoute;
    public Material adventurerUIUnitRouteCurve;
    public Material adventurerUIUnitRouteArrow;
    public Material adventurerUICursor;

    public int routeToX;
    public int routeToZ;

    //This game object is to record the location of the 2 count
    //path when it is reset to 0 this is used to remember what tile to disable
    public GameObject quadThatIsOneAwayFromUnit;

    public bool aiTurnStarted = false;

    public void Start()
    {
        currentFaction = 0;
        
        unitPathToCursor = new List<Node>();
        unitPathExists = false;

        TM = GetComponent<TileMap>();
    }

    public void Update()
    {
        if (currentFaction == 1 && aiTurnStarted == false)
        {
            aiTurnStarted = true;
            AIM.AdventurersTurn();
        }
        //Always trying to see where the mouse is pointing
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out hit))
        {
            //Update cursorLocation and unit appearing in the topLeft
            cursorUIUpdate();

            //If the unit is selected we want to highlight the current path with the UI
            if (TM.selectedUnit != null && TM.selectedUnit.GetComponent<Unit>().getMovementStateEnum(1) == TM.selectedUnit.GetComponent<Unit>().unitMoveState && !aiTurnStarted)
            {
                //Check to see if the cursor is in range, we can't show movement outside of the range so there is no point if it's outside
                if (TM.selectedUnitMoveRange.Contains(TM.graph[cursorX, cursorZ]))
                {
                    //Generate new path to cursor, trying to limit this to once per new cursor location or else it's too many calculations
                    if (cursorX != TM.selectedUnit.GetComponent<Unit>().tileX || cursorZ != TM.selectedUnit.GetComponent<Unit>().tileZ)
                    {
                        if (!unitPathExists && TM.selectedUnit.GetComponent<Unit>().movementQueue.Count == 0)
                        {
                            unitPathToCursor = GenerateCursorRouteTo(cursorX, cursorZ);
                            routeToX = cursorX;
                            routeToZ = cursorZ;

                            if (unitPathToCursor.Count != 0) 
                            {
                                for (int i = 0; i < unitPathToCursor.Count; i++)
                                {
                                    int nodeX = unitPathToCursor[i].x;
                                    int nodeZ = unitPathToCursor[i].z;

                                    if (i == 0)
                                    {
                                        GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
                                        quadToUpdate.GetComponent<Renderer>().material = UICursor;
                                    }
                                    else if (i != 0 && (i + 1) != unitPathToCursor.Count)
                                    {
                                        //This is used to set the indicator for tiles excluding the first/last tile
                                        setCorrectRouteWithInputAndOutout(nodeX, nodeZ, i, true);
                                    }
                                    else if (i == unitPathToCursor.Count - 1)
                                    {
                                        //This is used to set the indicator for the final tile
                                        SetCorrectRouteFinalTile(nodeX, nodeZ, i, true);
                                    }
                                    TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ].GetComponent<Renderer>().enabled = true;
                                }
                            }
                            unitPathExists = true;
                        }

                        else if (routeToX != cursorX || routeToZ != cursorZ)
                        {
                            if (unitPathToCursor.Count != 0)
                            {
                                for (int i = 0; i < unitPathToCursor.Count; i++)
                                {
                                    int nodeX = unitPathToCursor[i].x;
                                    int nodeZ = unitPathToCursor[i].z;
                                    TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ].GetComponent<Renderer>().enabled = false;
                                }
                            }
                            unitPathExists = false;
                        }
                    }
                    else if (cursorX == TM.selectedUnit.GetComponent<Unit>().tileX && cursorZ == TM.selectedUnit.GetComponent<Unit>().tileZ)
                    {
                        TM.disableUnitUIRoute();
                        unitPathExists = false;
                    }
                }
            }
        }
    }

    //IN:
    //OUT: void
    //DESC: icnrements the current faction
    public void SwitchCurrentFaction()
    {
        ResetUnitsMovements(returnFaction(currentFaction));
        currentFaction++;
        if (currentFaction == numberOfFactions)
        {
            currentFaction = 0;
            aiTurnStarted= false;
        }
    }

    //IN: int i, the index for each faction
    //OUT: gameObject faction
    //DESC: return the gameObject of the requested faction
    public GameObject returnFaction(int i)
    {
        GameObject factionToReturn = null;
        if (i == 0)
        {
            factionToReturn = factionMonsters;
        }
        else if (i == 1)
        {
            factionToReturn = factionAdventurers;
        }
        return factionToReturn;
    }

    //IN: gameObject team - used to reset (re-enable) all the unit movements
    //OUT: void
    //DESC: re-enables mvoement for all units on the team
    public void ResetUnitsMovements(GameObject factionToReset)
    {
        foreach (Transform unit in factionToReset.transform)
        {
            unit.GetComponent<Unit>().MoveAgain();
        }
    }
    
    //IN:
    //OUT: void
    //DESC: ends the turn
    public void EndTurn()
    {
        if (TM.selectedUnit == null)
        {
            SwitchCurrentFaction();
        }
    }

    //IN: Attacking unit and receiving unit
    //OUT: void
    //DESC: checks to see if units remain on a team
    public void CheckIfUnitsRemain(GameObject initiatorUnit, GameObject receiverUnit)
    {
        StartCoroutine(CheckIfUnitsRemainCoroutine(initiatorUnit, receiverUnit));
    }

    //IN:
    //OUT: void
    //DESC: updates the cursor for the UI
    public void cursorUIUpdate()
    {
        //If we are mousing over a tile, highlight it
        if (hit.transform.CompareTag("Tile"))
        {
            if (tileBeingDisplayed == null)
            {
                selectedXTile = hit.transform.gameObject.GetComponent<ClickableTile>().tileX;
                selectedZTile = hit.transform.gameObject.GetComponent<ClickableTile>().tileZ;
                cursorX = selectedXTile;
                cursorZ = selectedZTile;
                TM.quadOnMapCursor[selectedXTile, selectedZTile]. GetComponent<MeshRenderer>().enabled = true;
                tileBeingDisplayed = hit.transform.gameObject;
            }
            else if (tileBeingDisplayed != hit.transform.gameObject)
            {
                selectedXTile = tileBeingDisplayed.GetComponent<ClickableTile>().tileX;
                selectedZTile = tileBeingDisplayed.GetComponent<ClickableTile>().tileZ;
                TM.quadOnMapCursor[selectedXTile, selectedZTile].GetComponent<MeshRenderer>().enabled = false;

                selectedXTile = hit.transform.gameObject.GetComponent<ClickableTile>().tileX;
                selectedZTile = hit.transform.gameObject.GetComponent<ClickableTile>().tileZ;
                cursorX = selectedXTile;
                cursorZ = selectedZTile;
                TM.quadOnMapCursor[selectedXTile, selectedZTile].GetComponent<MeshRenderer>().enabled = true;
                tileBeingDisplayed = hit.transform.gameObject;
            }
        }
        //If we are mousing over a unit, highlight the tile that the unit is occupying
        else if (hit.transform.CompareTag("Unit"))
        {
            if (tileBeingDisplayed == null)
            {
                selectedXTile = hit.transform.parent.gameObject.GetComponent<Unit>().tileX;
                selectedZTile = hit.transform.parent.gameObject.GetComponent<Unit>().tileZ;
                cursorX = selectedXTile;
                cursorZ = selectedZTile;
                TM.quadOnMapCursor[selectedXTile, selectedZTile].GetComponent<MeshRenderer>().enabled = true;
                tileBeingDisplayed = hit.transform.parent.gameObject.GetComponent<Unit>().tileBeingOccupied;
            }
            else if (tileBeingDisplayed != hit.transform.gameObject)
            {
                if (hit.transform.parent.gameObject.GetComponent<Unit>().movementQueue.Count == 0)
                {
                    selectedXTile = tileBeingDisplayed.GetComponent<ClickableTile>().tileX;
                    selectedZTile = tileBeingDisplayed.GetComponent<ClickableTile>().tileZ;
                    TM.quadOnMapCursor[selectedXTile, selectedZTile].GetComponent<MeshRenderer>().enabled = false;

                    selectedXTile = hit.transform.parent.gameObject.GetComponent<Unit>().tileX;
                    selectedZTile = hit.transform.parent.gameObject.GetComponent<Unit>().tileZ;
                    cursorX = selectedXTile;
                    cursorZ = selectedZTile;
                    TM.quadOnMapCursor[selectedXTile, selectedZTile].GetComponent<MeshRenderer>().enabled = true;
                    tileBeingDisplayed = hit.transform.parent.gameObject.GetComponent<Unit>().tileBeingOccupied;
                }
            }
        }

        //We aren't pointing at anything so no cursor
        else
        {
            Debug.Log("oep");
            TM.quadOnMapCursor[selectedXTile, selectedZTile].GetComponent<MeshRenderer>().enabled = false;
        }
    }

    //IN: x and z location to go to
    //OUT: list of nodes to traverse
    //DESC: generate the cursor route to a position x, z
    public List<Node> GenerateCursorRouteTo(int x, int z)
    {
        if (TM.selectedUnit.GetComponent<Unit>().tileX == x && TM.selectedUnit.GetComponent<Unit>().tileZ == z)
        {
            currentPathForUnitRoute = new List<Node>();
            return currentPathForUnitRoute;
        }
        if (TM.UnitCanEnterTile(x, z) == false)
        {
            //Can't move into something so we can probably just return
            //Can't set this endpoint as our goal
            return null;
        }

        currentPathForUnitRoute = null;

        //Dijkstra's method
        Dictionary<Node, float> dist = new Dictionary<Node, float>();
        Dictionary<Node, Node> prev = new Dictionary<Node, Node>();
        Node source = TM.graph[TM.selectedUnit.GetComponent<Unit>().tileX, TM.selectedUnit.GetComponent<Unit>().tileZ];
        Node target = TM.graph[x, z];
        dist[source] = 0;
        prev[source] = null;
        //Unchecked nodes
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
            return null;
        }
        currentPathForUnitRoute = new List<Node>();
        Node curr = target;
        //Step through the current path and add it to the chain
        while (curr != null)
        {
            currentPathForUnitRoute.Add(curr);
            curr = prev[curr];
        }
        //Now currentPath is from target to source, we need to reverse it from source to target
        currentPathForUnitRoute.Reverse();
        return currentPathForUnitRoute;
    }

    //IN: two gameObjects current vector and the next on ine the list
    //OUT: Vector which is the direction between the two inputs
    //DESC: the direction from current to the next vector is returned
    public Vector2 directionBetween(Vector2 currentVector, Vector2 nextVector)
    {
        Vector2 vectorDirection = (nextVector - currentVector).normalized; 
        if (vectorDirection == Vector2.right) { return Vector2.right; }
        else if (vectorDirection == Vector2.left) { return Vector2.left; }
        else if (vectorDirection == Vector2.up) { return Vector2.up; }
        else if (vectorDirection == Vector2.down) { return Vector2.down; }
        else
        {
            Vector2 vectorToReturn = new Vector2();
            return vectorToReturn;
        }
    }

    //IN: two nodes that are being checked and int i is the position in the path ie i=0 is the first thing in the list
    //OUT: void
    //DESC: orients the quads to display proper information
    public void setCorrectRouteWithInputAndOutout(int nodeX, int nodeZ, int i, bool player)
    {
        Vector2 previousTile = new Vector2(0, 0);
        Vector2 currentTile = new Vector2(0, 0);
        Vector2 nextTile = new Vector2(0, 0);

        if (player == true)
        {
            previousTile = new Vector2(unitPathToCursor[i - 1].x + 1, unitPathToCursor[i - 1].z + 1);
            currentTile = new Vector2(unitPathToCursor[i].x + 1, unitPathToCursor[i].z + 1);
            nextTile = new Vector2(unitPathToCursor[i + 1].x + 1, unitPathToCursor[i + 1].z + 1);
        }
        else
        {
            previousTile = new Vector2(AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i - 1].x, AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i - 1].z);
            currentTile = new Vector2(AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i].x, AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i].z);
            nextTile = new Vector2(AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i + 1].x, AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i + 1].z);
        }
        Vector2 backToCurrentVector = directionBetween(previousTile, currentTile);
        Vector2 currentToFrontVector = directionBetween(currentTile, nextTile);

        //Right (UP/DOWN/RIGHT)
        if (backToCurrentVector == Vector2.right && currentToFrontVector == Vector2.right)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 270);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if (backToCurrentVector == Vector2.right && currentToFrontVector == Vector2.up)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 180);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if (backToCurrentVector == Vector2.right && currentToFrontVector == Vector2.down)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 270);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        if (backToCurrentVector == Vector2.right && currentToFrontVector == Vector2.left)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 90);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        //LEFT (UP/DOWN/LEFT)
        else if (backToCurrentVector == Vector2.left && currentToFrontVector == Vector2.left)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 90);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.left && currentToFrontVector == Vector2.up)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 90);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.left && currentToFrontVector == Vector2.down)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 0);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if (backToCurrentVector == Vector2.left && currentToFrontVector == Vector2.right)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 270);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        //UP (UP/RIGHT/LEFT)
        else if(backToCurrentVector == Vector2.up && currentToFrontVector == Vector2.up)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 0);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.up && currentToFrontVector == Vector2.right)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 0);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.up && currentToFrontVector == Vector2.left)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 270);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if (backToCurrentVector == Vector2.up && currentToFrontVector == Vector2.down)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 180);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        //DOWN (DOWN/RIGHT/LEFT)
        else if(backToCurrentVector == Vector2.down && currentToFrontVector == Vector2.down)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 0);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.down && currentToFrontVector == Vector2.right)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 90);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.down && currentToFrontVector == Vector2.left)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 180);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteCurve; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteCurve; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if (backToCurrentVector == Vector2.down && currentToFrontVector == Vector2.up)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 180);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRoute; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRoute; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
    }

    //IN: Two nodes that are being checked and int i is the position in the path ie i=0 is the first thing in the list
    //OUT: void
    //DESC: orients the quad for the final node in list to display proper information
    public void SetCorrectRouteFinalTile(int nodeX, int nodeZ, int i, bool player) 
    {
        Vector2 previousTile = new Vector2(0, 0);
        Vector2 currentTile = new Vector2(0, 0);

        if (player == true)
        {
            previousTile = new Vector2(unitPathToCursor[i - 1].x + 1, unitPathToCursor[i - 1].z + 1);
            currentTile = new Vector2(unitPathToCursor[i].x + 1, unitPathToCursor[i].z + 1);
        }
        else
        {
            previousTile = new Vector2(AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i - 1].x + 1, AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i - 1].z + 1);
            currentTile = new Vector2(AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i].x + 1, AIM.routeToMonsterUnit[(int)AIM.remainingMovement - i].z + 1);
        }

        Vector2 backToCurrentVector = directionBetween(previousTile, currentTile);

        if (backToCurrentVector == Vector2.right)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 270);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteArrow; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteArrow; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.left)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 90);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteArrow; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteArrow; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.up)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 0);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteArrow; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteArrow; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
        else if(backToCurrentVector == Vector2.down)
        {
            GameObject quadToUpdate = TM.quadOnMapForUnitMovementDisplay[nodeX, nodeZ];
            quadToUpdate.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 180);
            if (aiTurnStarted) { quadToUpdate.GetComponent<Renderer>().material = adventurerUIUnitRouteArrow; }
            else { quadToUpdate.GetComponent<Renderer>().material = UIunitRouteArrow; }
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
    }

    //IN: two units that last fought
    //OUT: void
    //DESC: waits until all the animations and stuff are finished before calling the game
    public IEnumerator CheckIfUnitsRemainCoroutine(GameObject initiatorUnit, GameObject receivingUnit)
    {
        while (initiatorUnit.GetComponent<Unit>().combatQueue.Count != 0)
        {
            yield return null;
        }

        while (receivingUnit.GetComponent<Unit>().combatQueue.Count != 0)
        {
            yield return new WaitForEndOfFrame();
        }
    }
}
