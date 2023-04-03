using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.WSA;

public class TileMap : MonoBehaviour
{
    [Header("Manager scripts")]
    public GameManagerScript GMS;
    public AdventurersAIManager AIM;

    //List of tiles that are used to generate the map
    [Header("Tiles")]
    public TileType[] tileTypes;
    int[,] tiles;

    //This is used when the game starts and there are pre-existing units
    //It uses this variable to check if there are any units and then maps them to the proper tiles
    [Header("Units on the board")]
    public GameObject unitsOnBoard;

    //This 2d array is the list of quadUI gameobjects on the board
    public GameObject[,] tilesOnMap;

    //This 2d array is the list of the quadUI gameobject on the board
    public GameObject[,] quadOnMap;
    public GameObject[,] quadOnMapForUnitMovementDisplay;
    public GameObject[,] quadOnMapCursor;

    [Header("UI for quads")]
    //Gameobject that is used to overlay onto the tiles to show possible movement
    public GameObject mapUI;
    //Gameobject that is used to highlight the mouse location
    public GameObject mapCursorUI;
    //Gameobject that is used to hightlight the path the unit is taking
    public GameObject mapUnitMovementUI;

    //Nodes along the path of shortest path from the pathfinding
    public List<Node> currentPath = null;

    //Node graph for pathfinding purposes
    public Node[,] graph;

    //Containers for the UI tiles
    [Header("Containers")]
    public GameObject tileContainer;
    public GameObject UIQuadPotentialMovesContainer;
    public GameObject UIQuadCursorContainer;
    public GameObject UIUnitMovementPathContainer;

    //Public area to set the size of the map
    [Header("Board size")]
    public int mapSizeX = 10;
    public int mapSizeZ = 10;

    [Header("Selected unit info")]
    public GameObject selectedUnit;
    public HashSet<Node> selectedUnitMoveRange;

    public bool unitSelected = false;

    public int unitSelectedPreviousX;
    public int unitSelectedPreviousZ;

    public GameObject previousOccupiedTile;

    //Public area to set the UI quad materials
    [Header("Materials")]
    public Material moveHighlight;

    private void Start()
    {
        GMS = GetComponent<GameManagerScript>();
        AIM = GetComponent<AdventurersAIManager>();
        GenerateMapData();
        GeneratePathfindingGraph();
        GenerateMapVisual();
        setIfTileIsOccupied();
        disableUnitUIRoute();
    }

    private void Update()
    {
        //If input is left mouse up then select the unit
        if (Input.GetMouseButtonDown(0) && GMS.aiTurnStarted == false)
        {
            if (selectedUnit == null)
            {
                mouseClickToSelectUnit();
            }
            //After a unit has been selected then if we get a mouse click, we need to check if the unit has entered the selection state (1) 'selected'
            //Move the unit
            else if (selectedUnit.GetComponent<Unit>().unitMoveState == selectedUnit.GetComponent<Unit>().getMovementStateEnum(1) && selectedUnit.GetComponent<Unit>().movementQueue.Count == 0)
            {
                if (selectTileToMoveTo())
                {
                    //Debug.Log("Movement path has been located");
                    unitSelectedPreviousX = selectedUnit.GetComponent<Unit>().tileX;
                    unitSelectedPreviousZ = selectedUnit.GetComponent<Unit>().tileZ;
                    previousOccupiedTile = selectedUnit.GetComponent<Unit>().tileBeingOccupied;
                    moveUnit();

                    //The moveUnit function calls a function on the Unit script when the movement is completed the finalization is called from that script
                    StartCoroutine(MoveUnitAndFinalize());
                }
            }
            //Finalize the movement
            else if (selectedUnit.GetComponent<Unit>().unitMoveState == selectedUnit.GetComponent<Unit>().getMovementStateEnum(2))
            {
                FinalizeOption();
            }
        }
        //Unselect unit with the right click
        if (Input.GetMouseButtonDown(1))
        {
            if (selectedUnit != null)
            {
                if (selectedUnit.GetComponent<Unit>().movementQueue.Count == 0)
                {
                    if (selectedUnit.GetComponent<Unit>().unitMoveState != selectedUnit.GetComponent<Unit>().getMovementStateEnum(3))
                    {
                        DeselectUnit();
                    }
                }
                else if (selectedUnit.GetComponent<Unit>().movementQueue.Count == 1)
                {
                    selectedUnit.GetComponent<Unit>().visualMovementSpeed = 0.5f;
                }
            }
        }
    }

    public float CostToEnterTile(int sourceX, int sourceY, int targetX, int targetY)
    {
        TileType tt = tileTypes[tiles[targetX, targetY]];
        float cost = tt.movementCost;

        if (UnitCanEnterTile(targetX, targetY) == false)
        {
            return Mathf.Infinity;
        }

        return cost;
    }

    void GenerateMapData()
    {
        //Allocate map tiles
        tiles = new int[mapSizeX, mapSizeZ];

        int x, z;

        //Initialize map tiles to be grass
        for (x = 0; x < mapSizeX; x++)
        {
            for (z = 0; z < mapSizeZ; z++)
            {
                tiles[x, z] = 0;
            }
        }

        ////Initialize group of swamp tiles
        //for (x = 3; x <= 5; x++)
        //{
        //    for (z = 0; z < 4; z++)
        //    {
        //        tiles[x, z] = 1;
        //    }
        //}

        //Builds a U-shaped mountain range
        tiles[4, 4] = 2;
        tiles[5, 4] = 2;
        tiles[6, 4] = 2;
        tiles[7, 4] = 2;
        tiles[8, 4] = 2;

        tiles[4, 5] = 2;
        tiles[4, 6] = 2;
        tiles[8, 5] = 2;
        tiles[8, 6] = 2;
    }

    //IN: Tile's x and z position
    //Out: Cost that is required to enter the tile
    //Desc: Checks what it costs for a unit to enter the tile
    public float CostToEnterTile(int targetX, int targetZ)
    {
        //If the tile is a wall movementcost is infinity to stop it
        if (UnitCanEnterTile(targetX, targetZ) == false)
        {
            return Mathf.Infinity;
        }

        //Get the movement cost here
        TileType tt = tileTypes[tiles[targetX, targetZ]];
        float cost = tt.movementCost;

        return cost;
    }

    //IN:
    //OUT: void
    //Desc: creates the graph for the pathfinding, sets up the neighbours
    void GeneratePathfindingGraph()
    {
        //Initialize the array
        graph = new Node[mapSizeX, mapSizeZ];

        //Initialize a Node for each spot in the array
        for (int x = 0; x < mapSizeX; x++)
        {
            for (int z = 0; z < mapSizeZ; z++)
            {
                graph[x, z] = new Node();
                graph[x, z].x = x;
                graph[x, z].z = z;
            }
        }

        //Now that all the nodes exist, calculate their neighbours
        for (int x = 0; x < mapSizeX; x++)
        {
            for (int z = 0; z < mapSizeZ; z++)
            {
                //This is the 4-way connection version:
                if (x > 0) {
                    graph[x, z].neighbours.Add(graph[x - 1, z]); }
                if (x < mapSizeX - 1) {
                    graph[x, z].neighbours.Add(graph[x + 1, z]); }

                if (z > 0) {
                    graph[x, z].neighbours.Add(graph[x, z - 1]); }
                if (z < mapSizeZ - 1) {
                    graph[x, z].neighbours.Add(graph[x, z + 1]); }
            }
        }
    }

    //IN:
    //OUT: Void
    //DESC: This instantiates all the information for the map, the UI quads and the map tiles
    void GenerateMapVisual()
    {
        //Generate list of actual tileGameObjectgs
        tilesOnMap = new GameObject[mapSizeX, mapSizeZ];
        quadOnMap = new GameObject[mapSizeX, mapSizeZ];
        quadOnMapForUnitMovementDisplay = new GameObject[mapSizeX, mapSizeZ];
        quadOnMapCursor = new GameObject[mapSizeX, mapSizeZ];
        int index;

        for (int x = 0; x < mapSizeX; x++)
        {
            for (int z = 0; z < mapSizeZ; z++)
            {
                index = tiles[x, z];
                GameObject newTile = Instantiate(tileTypes[index].tileVisualPrefab, new Vector3(x, 0, z), Quaternion.identity);

                //Gives each tile their own location
                ClickableTile ct = newTile.GetComponent<ClickableTile>();
                ct.tileX = x;
                ct.tileZ = z;
                ct.map = this;
                ct.transform.SetParent(tileContainer.transform);
                tilesOnMap[x, z] = newTile;

                GameObject gridUI = Instantiate(mapUI, new Vector3(x, 0.501f, z), Quaternion.Euler(90f, 0, 0));
                gridUI.transform.SetParent(UIQuadPotentialMovesContainer.transform);
                quadOnMap[x, z] = gridUI;

                GameObject gridUIForPathfindingDisplay = Instantiate(mapUnitMovementUI, new Vector3(x, 0.502f, z), Quaternion.Euler(90f, 0, 0));
                gridUIForPathfindingDisplay.transform.SetParent(UIUnitMovementPathContainer.transform);
                quadOnMapForUnitMovementDisplay[x, z] = gridUIForPathfindingDisplay;

                GameObject gridUICursor = Instantiate(mapCursorUI, new Vector3(x, 0.503f, z), Quaternion.Euler(90f, 0, 0));
                gridUICursor.transform.SetParent(UIQuadCursorContainer.transform);
                quadOnMapCursor[x, z] = gridUICursor;
            }
        }
    }

    //IN:
    //OUT: Void
    //DESC: Tells the selected unit to start moving
    public void moveUnit()
    {
        if (selectedUnit != null)
        {
            selectedUnit.GetComponent<Unit>().MoveNextTile();
        }
    }

    //IN: The x and z of a tile
    //Out: Vector 3 of the tile in world sapce
    //Desc: Returns a vector 3 of the tile in world space
    public Vector3 TileCoordToWorldCoord(int x, int z)
    {
        return new Vector3(x, 0, z);
    }

    //IN:
    //OUT: void
    //Desc: sets the tile as occupied if a unit is on the tile
    public void setIfTileIsOccupied()
    {
        foreach (Transform faction in unitsOnBoard.transform)
        {
            foreach (Transform unit in faction)
            {
                int unitX = unit.GetComponent<Unit>().tileX;
                int unitZ = unit.GetComponent<Unit>().tileZ;
                unit.GetComponent<Unit>().tileBeingOccupied = tilesOnMap[unitX, unitZ];
                tilesOnMap[unitX, unitZ].GetComponent<ClickableTile>().unitOnTile = unit.gameObject;
            }
        }
    }

    //IN: x and z position of the tile to move to
    //OUT: void
    //DESC: generates the path for the selected unit
    public void generatePathTo(int x, int z)
    {
        if (selectedUnit.GetComponent<Unit>().tileX == x && selectedUnit.GetComponent<Unit>().tileZ == z)
        {
            currentPath = new List<Node>();
            selectedUnit.GetComponent<Unit>().path = currentPath;

            return;
        }
        if (UnitCanEnterTile(x, z) == false)
        {
            //Can't move into something so we can probably just return
            return;
        }

        selectedUnit.GetComponent<Unit>().path = null;
        currentPath = null;
        //Dijkstra's path finding
        Dictionary<Node, float> dist = new Dictionary<Node, float>();
        Dictionary<Node, Node> prev = new Dictionary<Node, Node>();
        Node source = graph[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ];
        Node target = graph[x, z];
        dist[source] = 0;
        prev[source] = null;
        //unchecked nodes
        List<Node> unvisited = new List<Node>();

        //Initialize
        foreach (Node n in graph)
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
        //If there is a node in the unvisited list let's check it
        while (unvisited.Count > 0)
        {
            //u will be the invisited node with the shortest distance
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
                float alt = dist[u] + CostToEnterTile(n.x, n.z);
                if (alt < dist[n])
                {
                    dist[n] = alt;
                    prev[n] = u;
                }
            }
        }
        //If we're here we found the shortest path or no path exists
        if (prev[target] == null)
        {
            //no route
            return;
        }
        currentPath = new List<Node>();
        Node curr = target;
        //Step through the current path and add it to the chain
        while (curr != null)
        {
            currentPath.Add(curr);
            curr = prev[curr];
        }
        //Now currPath is from target to our source, we need to reverse it from source to target
        currentPath.Reverse();

        selectedUnit.GetComponent<Unit>().path = currentPath;
    }


    //IN: Tile's x and z position
    //OUT: True or false if the unit can enter the tile that was entered
    //DESC: if the tile is not occupied by another team's unit, then you can walk through and if the tile is walkable
    public bool UnitCanEnterTile(int x, int z)
    {
        if (tilesOnMap[x, z].GetComponent<ClickableTile>().unitOnTile != null)
        {
            if (tilesOnMap[x, z].GetComponent<ClickableTile>().unitOnTile.GetComponent<Unit>().factionNumber != selectedUnit.GetComponent<Unit>().factionNumber && selectedUnit.GetComponent<Unit>().factionNumber == 0)
            {
                return false;
            }
        }
        return tileTypes[tiles[x, z]].isWalkable;
    }

    //IN:
    //OUT: void
    //DESC: highlights the units range options
    //Attack option highlights need to be added here as well
    public void highlightUnitRange()
    {
        HashSet<Node> finalMovementHighlight = new HashSet<Node>();

        Node unitInitialNode = graph[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ];
        finalMovementHighlight = GetUnitMovementOptions();

        highlightMovementRange(finalMovementHighlight);

        selectedUnitMoveRange = finalMovementHighlight;
    }

    //IN:
    //OUT: HashSet<Node> of the tiles that can be reached by unit
    //DESC: Returns the HashSet of nodes that the unit can reach from its position
    public HashSet<Node> GetUnitMovementOptions()
    {
        float[,] cost = new float[mapSizeX, mapSizeZ];
        HashSet<Node> UIHighlight = new HashSet<Node>();
        HashSet<Node> tempUIHighlight = new HashSet<Node>();
        HashSet<Node> finalMovementHighlight = new HashSet<Node>();
        int moveSpeed = selectedUnit.GetComponent<Unit>().moveSpeed;
        Node unitInitialNode = graph[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ];

        // Set-up the initial costs for the neighbouring nodes
        foreach (Node n in unitInitialNode.neighbours)
        {
            cost[n.x, n.z] = CostToEnterTile(n.x, n.z);

            if (moveSpeed - cost[n.x, n.z] >= 0)
            {
                UIHighlight.Add(n);
            }
        }

        finalMovementHighlight.UnionWith(UIHighlight);

        int x = 0;
        while (UIHighlight.Count != 0)
        {
            foreach (Node n in UIHighlight)
            {
                foreach (Node neighbour in n.neighbours)
                {
                    if (!finalMovementHighlight.Contains(neighbour) && neighbour != unitInitialNode)
                    {
                        cost[neighbour.x, neighbour.z] = CostToEnterTile(neighbour.x, neighbour.z) + cost[n.x, n.z];

                        if (moveSpeed - cost[neighbour.x, neighbour.z] >= 0)
                        {
                            tempUIHighlight.Add(neighbour);
                        }
                    }
                }
            }

            UIHighlight = tempUIHighlight;
            finalMovementHighlight.UnionWith(UIHighlight);
            tempUIHighlight = new HashSet<Node>();
            x += 1;
        }
        return finalMovementHighlight;
    }

    //IN: Hash set of the avaialabe nodes that the unit can reach
    //OUT: void - it changes the quadUI property in the gameworld to visualize the selected unit's movement
    //DESC: This function highlights the selected unit's movement range
    public void highlightMovementRange(HashSet<Node> movementToHighlight)
    {
        foreach (Node n in movementToHighlight)
        {
            quadOnMap[n.x, n.z].GetComponent<Renderer>().material = moveHighlight;
            quadOnMap[n.x, n.z].GetComponent<MeshRenderer>().enabled = true;
        }
    }

    //IN:
    //OUT: void
    //DESC: finalizes the movement, sets the tile the unit moved to as occupied, etc
    public void finalizeMovementPosition()
    {
        tilesOnMap[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ].GetComponent<ClickableTile>().unitOnTile = selectedUnit;
        //After a unit has been moved we will set the unitMoveState to (2) the 'Moved' state
        selectedUnit.GetComponent<Unit>().setMovementState(2);

        HashSet<Node> occupiedTile = new HashSet<Node>();
        occupiedTile.Add(graph[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ]);
        highlightMovementRange(occupiedTile);
    }

    //IN:
    //OUT:
    //DESC: selects a unit based on the cursor from the other script
    public void mouseClickToSelectUnit()
    {
        if (selectedUnit == false && GMS.tileBeingDisplayed != null)
        {
            if (GMS.tileBeingDisplayed.GetComponent<ClickableTile>().unitOnTile != null)
            {
                GameObject tempSelectedUnit = GMS.tileBeingDisplayed.GetComponent<ClickableTile>().unitOnTile;
                if (tempSelectedUnit.GetComponent<Unit>().unitMoveState == tempSelectedUnit.GetComponent<Unit>().getMovementStateEnum(0)
                    && tempSelectedUnit.GetComponent<Unit>().factionNumber == GMS.currentFaction)
                {
                    disableHighlightUnitRange();
                    selectedUnit = tempSelectedUnit;
                    selectedUnit.GetComponent<Unit>().map = this;
                    selectedUnit.GetComponent<Unit>().setMovementState(1);
                    unitSelected = true;
                    highlightUnitRange();
                }
            }
        }
    }

    //IN: int number of adventurer to select
    //OUT: void
    //DESC: selects adventurer unit for the AI from the list of available units according to the AdventurersAIManager script
    public void AdventurerUnitToSelect(int unitNumber)
    {
        if (selectedUnit == false)
        {
            disableHighlightUnitRange();
            selectedUnit = AIM.adventurersUnitsLeft[unitNumber];
            selectedUnit.GetComponent<Unit>().map = this;
            selectedUnit.GetComponent<Unit>().setMovementState(1);
            unitSelected = true;
            highlightUnitRange();
            GameObject quadToUpdate = quadOnMapForUnitMovementDisplay[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ];
            quadToUpdate.GetComponent<Renderer>().material = GMS.adventurerUICursor;
            quadToUpdate.GetComponent<Renderer>().enabled = true;
        }
    }

    //IN:
    //OUT: void
    //DESC: finalizes the player's option, wait or attack
    //For later when I'm adding attacks
    public void FinalizeOption()
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out hit))
        {
            //This portion is to ensure that the tile has been clicked
            //If the tile has been clicked then we need to check if there is a unit on it
            if (hit.transform.gameObject.CompareTag("Tile"))
            {
                if (hit.transform.GetComponent<ClickableTile>().unitOnTile != null)
                {
                    GameObject unitOnTile = hit.transform.GetComponent<ClickableTile>().unitOnTile;
                    int unitX = unitOnTile.GetComponent<Unit>().tileX;
                    int unitZ = unitOnTile.GetComponent<Unit>().tileZ;

                    if (unitOnTile == selectedUnit)
                    {
                        disableHighlightUnitRange();
                        selectedUnit.GetComponent<Unit>().Wait();
                        selectedUnit.GetComponent<Unit>().setMovementState(3);
                        DeselectUnit();
                    }
                    //Add attack here
                }
                
            }
            else if (hit.transform.parent != null && hit.transform.parent.gameObject.CompareTag("Unit"))
            {
                GameObject unitClicked = hit.transform.parent.gameObject;
                int unitX = unitClicked.GetComponent<Unit>().tileX;
                int unitZ = unitClicked.GetComponent<Unit>().tileZ;

                if (unitClicked == selectedUnit)
                {
                    disableHighlightUnitRange();
                    selectedUnit.GetComponent<Unit>().Wait();
                    selectedUnit.GetComponent<Unit>().setMovementState(3);
                    DeselectUnit();
                }
                //Add attack here
            }
        }
    }

    //IN:
    //OUT: void
    //Desc: Finalizes the ai's option, wait or attack
    public void FinalizeAIOption()
    {
        disableHighlightUnitRange();
        selectedUnit.GetComponent<Unit>().Wait();
        selectedUnit.GetComponent<Unit>().setMovementState(3);
        DeselectUnit();
    }

    //IN:
    //Out: void
    //DESC: de-selects the unit
    public void DeselectUnit()
    {
        if (selectedUnit != null)
        {
            if (selectedUnit.GetComponent<Unit>().unitMoveState == selectedUnit.GetComponent<Unit>().getMovementStateEnum(1))
            {
                disableHighlightUnitRange();
                disableUnitUIRoute();
                selectedUnit.GetComponent<Unit>().setMovementState(0);

                selectedUnit = null;
                unitSelected = false;
            }
            else if (selectedUnit.GetComponent<Unit>().unitMoveState == selectedUnit.GetComponent<Unit>().getMovementStateEnum(3))
            {
                disableHighlightUnitRange();
                disableUnitUIRoute();
                selectedUnit = null;
                unitSelected = false;
            }
            else
            {
                disableHighlightUnitRange();
                disableUnitUIRoute();
                tilesOnMap[selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileZ].GetComponent<ClickableTile>().unitOnTile = null;
                tilesOnMap[unitSelectedPreviousX, unitSelectedPreviousZ].GetComponent<ClickableTile>().unitOnTile = selectedUnit;

                selectedUnit.GetComponent<Unit>().tileX = unitSelectedPreviousX;
                selectedUnit.GetComponent<Unit>().tileZ = unitSelectedPreviousZ;
                selectedUnit.GetComponent<Unit>().tileBeingOccupied = previousOccupiedTile;
                selectedUnit.transform.position = TileCoordToWorldCoord(unitSelectedPreviousX, unitSelectedPreviousZ);
                selectedUnit.GetComponent<Unit>().setMovementState(0);
                selectedUnit = null;
                unitSelected = false;
            }
        }
    }

    //IN:
    //OUT: void
    //Desc: disables the highlight
    public void disableHighlightUnitRange()
    {
        foreach (GameObject quad in quadOnMap)
        {
            if (quad.GetComponent<Renderer>().enabled == true)
            {
                quad.GetComponent<Renderer>().enabled = false; 
            }
        }
    }

    //IN:
    //OUT: void
    //DESC: disables the quads that are being used to highlight the position
    public void disableUnitUIRoute()
    {
        foreach (GameObject quad in quadOnMapForUnitMovementDisplay)
        {
            if (quad.GetComponent<Renderer>().enabled == true)
            {
                quad.GetComponent<Renderer>().enabled = false;
            }
        }
    }

    //IN:
    //OUT: void
    //Desc: mvoes the unit then finalizes the movement
    public IEnumerator MoveUnitAndFinalize()
    {
        disableHighlightUnitRange();
        disableUnitUIRoute();
        while (selectedUnit.GetComponent<Unit>().movementQueue.Count != 0)
        {
            yield return new WaitForEndOfFrame();
        }
        finalizeMovementPosition();
    }

    //IN:
    //OUT: true if there is a tile that was clicked that the unit can move to, false otherwise
    //DESC: checks if the tile that was clicked is move-able for the selected unit
    public bool selectTileToMoveTo()
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform.gameObject.CompareTag("Tile"))
            {
                int clickedTileX = hit.transform.GetComponent<ClickableTile>().tileX;
                int clickecTileZ = hit.transform.GetComponent<ClickableTile>().tileZ;
                Node nodeToCheck = graph[clickedTileX, clickecTileZ];

                if (selectedUnitMoveRange.Contains(nodeToCheck))
                {
                    if ((hit.transform.gameObject.GetComponent<ClickableTile>().unitOnTile == null || hit.transform.gameObject.GetComponent<ClickableTile>().unitOnTile == selectedUnit) 
                        && selectedUnitMoveRange.Contains(nodeToCheck))
                    {
                        generatePathTo(clickedTileX, clickecTileZ);
                        return true;
                    }
                }
            }
            else if (hit.transform.gameObject.CompareTag("Unit"))
            {
                if (hit.transform.parent.gameObject == selectedUnit)
                {
                    generatePathTo(selectedUnit.GetComponent<Unit>().tileX, selectedUnit.GetComponent<Unit>().tileX);
                    return true;
                }
            }
        }
        return false;
    }
}