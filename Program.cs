using System;
using System.Linq;
using System.Collections.Generic;

namespace Game
{
    static class Player
    {
        public static int Row = 12;
        public static int Col = 10;

        static void Main(string[] args)
        {
            string[] inputs;
            inputs = Console.ReadLine().Split(' ');
            int width = int.Parse(inputs[0]);
            int height = int.Parse(inputs[1]);
            int myId = int.Parse(inputs[2]);
            List<string> singlePatterns = new List<string>
            {"0", "1", "2"};
            List<string> grid = new List<string>();
            List<Coordinates> quadTargetList = new List<Coordinates>();
            List<Coordinates> trioTargetList = new List<Coordinates>();
            List<Coordinates> duoTargetList = new List<Coordinates>();
            List<Coordinates> singleTargetList = new List<Coordinates>();
            List<Tile> allReachableTiles = new List<Tile>();
            List<Bomb> bombList = new List<Bomb>();
            Coordinates currentCoordinates = new Coordinates(0, 0);
            Target target = new Target(0, new Coordinates(0, 0));
            Item closestItem = new Item(1, new Coordinates(0, 0));
            List<Coordinates> path = new List<Coordinates>();
            List<Coordinates> pathToSafety = new List<Coordinates>();
            List<Coordinates> itemPath = new List<Coordinates>();
            List<Item> itemList = new List<Item>();
            List<Coordinates> bombedTargets = new List<Coordinates>();
            List<PlayerEntity> allPlayers = new List<PlayerEntity>();
            int distanceToTarget = 99;
            int currentExplosionRadius = 3;
            int bombCount = 1;
            int maxBombAmount = 0;
            int distanceToItem;
            bool allTilesExplodeAtTheSameTime = false;
            bool safeToPlaceBomb;
            bool moveToSafetyBool = false;
            bool endgame = false;
            int turn = 0;

            while (true)
            {
                turn++;
                grid.Clear();
                allReachableTiles.Clear();
                quadTargetList.Clear();
                trioTargetList.Clear();
                duoTargetList.Clear();
                singleTargetList.Clear();
                bombList.Clear();
                itemList.Clear();
                allPlayers.Clear();
                distanceToItem = 99;

                for (int i = 0; i < height; i++)
                {
                    string rowString = Console.ReadLine();
                    //Creates a grid
                    grid.Add(rowString);

                    //Populates singleTargetList with all boxes
                    PopulateTargetListWithSingleBoxTargets(rowString, i, singlePatterns, singleTargetList, bombedTargets);
                }

                int entities = int.Parse(Console.ReadLine());

                for (int i = 0; i < entities; i++)
                {
                    inputs = Console.ReadLine().Split(' ');
                    int entityType = int.Parse(inputs[0]);
                    int owner = int.Parse(inputs[1]);
                    int x = int.Parse(inputs[2]);
                    int y = int.Parse(inputs[3]);
                    int param1 = int.Parse(inputs[4]);
                    int param2 = int.Parse(inputs[5]);

                    //get player coords
                    if (entityType == 0)
                    {
                        //if player is me
                        if (owner == myId)
                        {
                            //Sets current coordinates and bombCount
                            currentCoordinates.X = x;
                            currentCoordinates.Y = y;
                            bombCount = param1;
                            currentExplosionRadius = param2;
                            //tracks maxBombAmount (not the best way)
                            if (bombCount > maxBombAmount)
                            {
                                maxBombAmount = bombCount;
                            }
                        }
                        //adds all players to list
                        allPlayers.Add(new PlayerEntity(new Coordinates(x, y), param2, owner));
                    }

                    //Adds bomb centers to bomb list
                    if (entityType == 1)
                    {
                        //Creates bomb at current location, param1 is turns until explosion
                        //param2 is explosion radius
                        Bomb bomb = new Bomb(new Coordinates(x, y), param1 - 1, param2);
                        //Creates the bomb center as 'C'
                        grid[y] = grid[y].Substring(0, x) + 'C' + grid[y].Substring(x + 1);
                        //Adds bombs with center coordinates to list
                        bombList.Add(bomb);
                    }

                    //Adds all items to itemList
                    if (entityType == 2 && param1 == 2)
                    {
                        Item addableItem = new Item(param1, new Coordinates(x, y));

                        if (!IsItemInBombsPath(bombList, addableItem))
                        {
                            itemList.Add(addableItem);
                        }
                    }
                    else if (entityType == 2 && param1 == 1)
                    {
                        Item addableItem = new Item(param1, new Coordinates(x, y));

                        if (!IsItemInBombsPath(bombList, addableItem))
                        {
                            itemList.Add(addableItem);
                        }
                    }
                }

                //Updates bomb list
                //Calculates bomb explosion radius
                //Stops at item or box
                UpdatesBombListAndBombedTargets(bombList, bombedTargets, itemList, grid);
                //Updates all target lists
                //Searches for targets up to 4 boxes
                allReachableTiles = FindAllReachableTiles(currentCoordinates, grid, bombList);
                UpdateAllTilesListWithVisibleBoxes(allReachableTiles, grid, currentExplosionRadius, bombedTargets, itemList);
                OrganizeTargetsIntoLists(quadTargetList, trioTargetList, duoTargetList, allReachableTiles);

                //if there are no more targets, switches to endgame
                if (quadTargetList.Count == 0 &&
                    trioTargetList.Count == 0 &&
                    duoTargetList.Count == 0 &&
                    singleTargetList.Count == 0)
                {
                    endgame = true;
                }

                //Calculates distance to nearest item, if there are any
                if (itemList.Count > 0 && maxBombAmount <= 3 && currentExplosionRadius <= 6)
                {
                    //prioritizes extra bombs above extra range
                    closestItem = ClosestItem(itemList, currentCoordinates, maxBombAmount <= 3 ? 2 : 1);
                    itemPath = BfsPathfinder(currentCoordinates, closestItem.Coords, grid, bombList);
                    distanceToItem = itemPath.Count;
                }

                //Selects target
                while (true)
                {
                    //if no targets, that are reachable found, moves to safety
                    if (quadTargetList.Count == 0 &&
                        trioTargetList.Count == 0 &&
                        duoTargetList.Count == 0 &&
                        singleTargetList.Count == 0)
                    {
                        if (endgame)
                        {
                            break;
                        }

                        moveToSafetyBool = true;
                        break;
                    }

                    target = SelectTarget(quadTargetList, trioTargetList, duoTargetList, singleTargetList, currentCoordinates, grid, bombList);
                    path = BfsPathfinder(currentCoordinates, target.Coords, grid, bombList);

                    //if target is a single box, removes the last coords (the boxes coordinates)
                    if (target.Value == 1 && path.Count != 0)
                    {
                        path.RemoveAt(path.Count - 1);
                    }

                    //Checks potential situation if bomb was placed at target
                    //to see if player can survive it
                    List<Coordinates> potentialPathToSafety = PotentialSituation(grid, target, bombList, itemList, bombedTargets, currentExplosionRadius).Item1;
                    Coordinates lastToExplodeTemp = PotentialSituation(grid, target, bombList, itemList, bombedTargets, currentExplosionRadius).Item3;

                    //if there is a path to target
                    //and and there is a potential path to safety
                    if (path.Count != 0 && potentialPathToSafety.Count != 0)
                    {
                        break;
                    }

                    //if current coordinates are target
                    if (currentCoordinates.X == target.Coords.X &&
                        currentCoordinates.Y == target.Coords.Y)
                    {
                        //and there is a path to safety
                        if (potentialPathToSafety.Count != 0)
                        {
                            break;
                        }
                        //or if dangers levels
                        if (lastToExplodeTemp.X != 20 &&
                            lastToExplodeTemp.Y != 20)
                        {
                            break;
                        }
                    }

                    //if there is a path to target and bomb count is 0, but no path to safety
                    if (path.Count != 0 && potentialPathToSafety.Count == 0 && bombCount == 0)
                    {
                        break;
                    }
                }

                //Shows current target in console
                Console.Error.WriteLine("Targeting : " + target.Coords.X + " " + target.Coords.Y);
                Console.Error.WriteLine("Current target value : " + target.Value);

                //At endgame constantly moves away from bomb centres
                if ((grid[currentCoordinates.Y][currentCoordinates.X] == 'C' && endgame))
                {
                    moveToSafetyBool = true;
                }

                Bomb bombResponsibleForExplosion;

                //if current location is 'B' and no more bombs left
                //checks if potential path to safety isnt empty and if pathToSafety is smaller than responisble bombs timer
                //or if danger levels
                if (grid[currentCoordinates.Y][currentCoordinates.X] == 'B')
                {
                    bombResponsibleForExplosion = FindBombResponisbleForExplosion(bombList, currentCoordinates);
                    safeToPlaceBomb = (PotentialSituation(grid, target, bombList, itemList, bombedTargets, currentExplosionRadius).Item1.Count != 0 &&
                                  PotentialSituation(grid, target, bombList, itemList, bombedTargets, currentExplosionRadius).Item1.Count < bombResponsibleForExplosion.TurnsUntilExplode) ||
                                  (!PotentialSituation(grid, target, bombList, itemList, bombedTargets, currentExplosionRadius).Item2 &&
                                  bombCount > 0);
                }
                //if current location is not 'B', checks if there is a path to safety
                else
                {
                    safeToPlaceBomb = PotentialSituation(grid, target, bombList, itemList, bombedTargets, currentExplosionRadius).Item1.Count != 0;
                }

                // creates a list of boxes next to player
                List<Coordinates> boxesNextToPlayer = IsNextToBox(currentCoordinates, grid, bombedTargets);
                //Checks if there is another player next to you
                bool nextToPlayer = ClosestPlayer(currentCoordinates, allPlayers, myId) <= 1;
                //Checks if you are in line with a target
                bool inLineWithTarget = target.Coords.X == currentCoordinates.X || target.Coords.Y == currentCoordinates.Y;
                distanceToTarget = Math.Abs(target.Coords.X - currentCoordinates.X) + Math.Abs(target.Coords.Y - currentCoordinates.Y);
                //Checks if there are no walls or items blocking target
                bool freeLineToTarget = FreeLineToTarget(grid, currentCoordinates, target, itemList);
                pathToSafety = PotentialSituation(grid, new Target(target.Value, currentCoordinates), bombList, itemList, bombedTargets, currentExplosionRadius).Item1;
                allTilesExplodeAtTheSameTime = PotentialSituation(grid, new Target(target.Value, currentCoordinates), bombList, itemList, bombedTargets, currentExplosionRadius).Item2;
                Coordinates lastToExplode = PotentialSituation(grid, new Target(target.Value, currentCoordinates), bombList, itemList, bombedTargets, currentExplosionRadius).Item3;

                //PvP is top priority
                if (nextToPlayer)
                {
                    //if there is no path to safety, bomb current coordinates
                    //otherwise bomb and run
                    if (pathToSafety.Count == 0)
                    {
                        pathToSafety = MoveToSafety(currentCoordinates, grid, bombList);

                        if (pathToSafety.Count <= 1)
                        {
                            if (grid[currentCoordinates.Y][currentCoordinates.X] == '.')
                            {
                                Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} STAND-STILL");
                            }
                            else
                            {
                                lastToExplode = LastTileToExplode(currentCoordinates, grid, bombList);
                                Console.WriteLine($"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"MOVE {pathToSafety[1].X} {pathToSafety[1].Y} RUN");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"BOMB {pathToSafety[1].X} {pathToSafety[1].Y} PVP-BOMB");
                    }
                }
                //if you have boxes near you and your bomb count is above 0
                //and it is safe to place a bomb and double target value is less than the path to the target
                //bomb the boxes next to you
                else if (boxesNextToPlayer.Count != 0 && bombCount > 0 && safeToPlaceBomb && target.Value * 2 < path.Count)
                {
                    //adds all boxes in boxesNextToPlayer into bombedTargets
                    bombedTargets.AddRange(boxesNextToPlayer);

                    //if potential path to safety is empty after bombing current location
                    if (pathToSafety.Count <= 1)
                    {
                        //if there will be no escape
                        if (!allTilesExplodeAtTheSameTime)
                        {
                            Console.WriteLine($"BOMB {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 1");
                        }
                        //if you have arrived at your target
                        else if (path.Count <= 1)
                        {
                            Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} WAITING-FOR-A-BOMB 1");
                        }
                        //else keep moving to target
                        else
                        {
                            Console.WriteLine($"MOVE {path[1].X} {path[1].Y} KEEP-GOING 1");
                        }
                    }
                    //potential path to safety is not empty, bomb it and run
                    else
                    {
                        Console.WriteLine($"BOMB {pathToSafety[1].X} {pathToSafety[1].Y} BOMBING-BOXES-NEXT-TO-ME 1");
                    }


                }
                //Safety is prioritized above endgame, items and target
                else if (moveToSafetyBool)
                {
                    pathToSafety = MoveToSafety(currentCoordinates, grid, bombList);

                    if (pathToSafety.Count != 0)
                    {
                        if (pathToSafety.Count <= 2)
                        {
                            moveToSafetyBool = false;
                        }

                        Console.WriteLine($"MOVE {pathToSafety[1].X} {pathToSafety[1].Y} RUN 2");
                    }
                    else
                    {
                        //if there is no path to safety
                        //and current location is a dangerous spot
                        //find the tile that explodes last
                        if (grid[currentCoordinates.Y][currentCoordinates.X] == 'B' ||
                            grid[currentCoordinates.Y][currentCoordinates.X] == 'C')
                        {
                            lastToExplode = LastTileToExplode(currentCoordinates, grid, bombList);
                            Console.WriteLine($"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 2");
                        }
                        else
                        {
                            //if current coords are not 'B' or 'C' then you are safe
                            moveToSafetyBool = false;
                            Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} STAND-STILL 2");
                        }
                    }
                }
                //Endgame is prioritized above item and target
                else if (endgame)
                {
                    //Creates a potential situation where all players drop a bomb
                    bombList.AddRange(allPlayers.Select(player => new Bomb(player.Coordinates, 8, player.ExploRadius)));

                    if (safeToPlaceBomb)
                    {
                        //if there is no potential path to safety, if everybody drops a bomb
                        if (pathToSafety.Count <= 1)
                        {
                            //generate the actual path to safety
                            pathToSafety = MoveToSafety(currentCoordinates, grid, bombList);

                            //if there is still no path
                            if (pathToSafety.Count <= 1)
                            {
                                Console.WriteLine(!allTilesExplodeAtTheSameTime
                                    ? $"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 3"
                                    : $"MOVE {currentCoordinates.X} {currentCoordinates.Y} WAIT 3");
                            }
                            else
                            {
                                Console.WriteLine($"MOVE {pathToSafety[1].X} {pathToSafety[1].Y} AVOID-DEATH 3");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"BOMB {pathToSafety[1].X} {pathToSafety[1].Y} ENDGAME-BOMBING 3");
                        }
                    }
                    //its not safe to place a bomb
                    else
                    {
                        //try to escape
                        moveToSafetyBool = true;
                        pathToSafety = MoveToSafety(currentCoordinates, grid, bombList);

                        if (grid[currentCoordinates.Y][currentCoordinates.X] == '.')
                        {
                            Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} STAY 3");
                        }
                        else
                        {
                            if (pathToSafety.Count <= 1)
                            {
                                lastToExplode = LastTileToExplode(currentCoordinates, grid, bombList);
                                Console.WriteLine($"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 3");
                            }
                            else
                            {
                                Console.WriteLine($"MOVE {pathToSafety[1].X} {pathToSafety[1].Y} RUN 3");
                            }
                        }
                    }
                }
                //Item pickup prioritized above target
                //if item is within 4 moves, go for it
                else if (distanceToItem <= 4 && distanceToItem > 0)
                {
                    Console.WriteLine(distanceToItem == 1
                        ? $"MOVE {closestItem.Coords.X} {closestItem.Coords.Y} PICKING-UP-ITEM 4"
                        : $"MOVE {itemPath[1].X} {itemPath[1].Y} MOVING-TO-ITEM 4");
                }
                //Target
                else
                {
                    //If target is more than 20 squares away, safety is turned on
                    if (path.Count > 20)
                    {
                        moveToSafetyBool = true;
                        Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} TARGET-TOO-FAR 5");
                    }
                    // if there is no path (arrived at target)
                    // or player is in line with the target
                    else if (path.Count <= 1 || (inLineWithTarget && distanceToTarget <= currentExplosionRadius - 1 && target.Value == 1 && freeLineToTarget))
                    {
                        //if bombs are available
                        if (bombCount > 0)
                        {
                            if (safeToPlaceBomb)
                            {
                                if (path.Count <= 1 && pathToSafety.Count == 0)
                                {
                                    Console.WriteLine(allTilesExplodeAtTheSameTime
                                        ? $"MOVE {currentCoordinates.X} {currentCoordinates.Y} WAIT 5"
                                        : $"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 5");
                                }
                                else if (pathToSafety.Count == 0)
                                {
                                    Console.WriteLine(allTilesExplodeAtTheSameTime
                                        ? $"MOVE {path[1].X} {path[1].Y} MOVE-TO-TARGET 5"
                                        : $"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 5");
                                }
                                else
                                {
                                    Console.WriteLine($"BOMB {pathToSafety[1].X} {pathToSafety[1].Y} BOMBING TARGET 5");
                                }
                            }
                            else
                            {
                                moveToSafetyBool = true;
                                pathToSafety = MoveToSafety(currentCoordinates, grid, bombList);

                                Console.WriteLine(pathToSafety.Count <= 1
                                    ? $"MOVE {currentCoordinates.X} {currentCoordinates.Y} STAY 5"
                                    : $"MOVE {pathToSafety[1].X} {pathToSafety[1].Y} RUN 5");
                            }
                        }
                        //if bombs are not available
                        else
                        {
                            //if you are on a normal tile, wait
                            if (grid[currentCoordinates.Y][currentCoordinates.X] == '.')
                            {
                                Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} NO-BOMBS-WAIT 5");
                            }
                            //if you are on 'B' or 'C'
                            else
                            {
                                //generate the actual path to safety
                                pathToSafety = MoveToSafety(currentCoordinates, grid, bombList);

                                //if there is no path to safety
                                if (pathToSafety.Count <= 1)
                                {
                                    lastToExplode = LastTileToExplode(currentCoordinates, grid, bombList);

                                    if (lastToExplode.X != 20 && lastToExplode.Y != 20)
                                    {
                                        Console.WriteLine($"MOVE {lastToExplode.X} {lastToExplode.Y} DANGER-LEVELS 5");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"MOVE {currentCoordinates.X} {currentCoordinates.Y} CERTAIN-DEATH 5");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"MOVE {pathToSafety[1].X} {pathToSafety[1].Y} RUN 5");
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"MOVE {path[1].X} {path[1].Y} MOVING-TO-TARGET 5");
                    }
                }

            }
        }

        static List<Coordinates> MoveToSafety(Coordinates currentCoordinates, List<string> grid, List<Bomb> bombList)
        {
            Queue<QueueCoordinates> queue = new Queue<QueueCoordinates>();
            List<QueueCoordinates> visitedCoordinates = new List<QueueCoordinates>();
            List<Coordinates> neighbors;
            Dictionary<QueueCoordinates, QueueCoordinates> predecessors = new Dictionary<QueueCoordinates, QueueCoordinates>();
            QueueCoordinates predecessor;
            bool reachable = false;
            QueueCoordinates currentTileBeingChecked = new QueueCoordinates(currentCoordinates);
            currentTileBeingChecked.Distance = 0;
            queue.Enqueue(currentTileBeingChecked);

            if (grid[currentCoordinates.Y][currentCoordinates.X] == '.')
            {
                return new List<Coordinates>();
            }

            while (queue.Count > 0)
            {
                currentTileBeingChecked = queue.Dequeue();

                if (!CollectionContainsQueueCoordinates(visitedCoordinates, currentTileBeingChecked.Coords))
                {
                    visitedCoordinates.Add(currentTileBeingChecked);

                }

                neighbors = Neighbors(currentTileBeingChecked.Coords);
                predecessor = currentTileBeingChecked;

                foreach (var neighbor in neighbors)
                {
                    if (grid[neighbor.Y][neighbor.X] == '.')
                    {
                        QueueCoordinates qCoords = new QueueCoordinates(neighbor);
                        qCoords.Distance = predecessor.Distance + 1;
                        predecessors.Add(qCoords, predecessor);
                        currentTileBeingChecked = qCoords;
                        reachable = true;
                        break;
                    }

                    if (grid[neighbor.Y][neighbor.X] != 'B')
                    {
                        continue;
                    }

                    Bomb responsibleBombForExplosion = FindBombResponisbleForExplosion(bombList, new Coordinates(neighbor.X, neighbor.Y));
                    int distanceToTile = Math.Abs(currentCoordinates.X - neighbor.X) + Math.Abs(currentCoordinates.Y - neighbor.Y);
                    int turnsUntilTileExplodes = responsibleBombForExplosion.TurnsUntilExplode - distanceToTile;

                    if (turnsUntilTileExplodes < 1)
                    {
                        continue;
                    }

                    if (!CollectionContainsQueueCoordinates(visitedCoordinates, neighbor) && !QueueContainsCoordinates(queue, neighbor))
                    {
                        QueueCoordinates qCoords = new QueueCoordinates(neighbor);
                        qCoords.Distance = predecessor.Distance + 1;
                        predecessors.Add(qCoords, predecessor);
                        queue.Enqueue(qCoords);
                    }
                }

                if (reachable)
                {
                    break;
                }
            }

            List<Coordinates> path = new List<Coordinates>();

            if (!reachable)
            {
                return path;

            }

            while (true)
            {
                path.Add(currentTileBeingChecked.Coords);
                if (currentTileBeingChecked.Coords.X == currentCoordinates.X && currentTileBeingChecked.Coords.Y == currentCoordinates.Y) break;
                currentTileBeingChecked = predecessors[currentTileBeingChecked];
            }

            path.Reverse();

            return path;
        }

        static List<Coordinates> BfsPathfinder(Coordinates currentCoordinates, Coordinates target, List<string> grid, List<Bomb> bombList)
        {
            Queue<QueueCoordinates> queue = new Queue<QueueCoordinates>();
            List<QueueCoordinates> visitedCoordinates = new List<QueueCoordinates>();
            List<Coordinates> neighbors;
            Dictionary<QueueCoordinates, QueueCoordinates> predecessors = new Dictionary<QueueCoordinates, QueueCoordinates>();
            QueueCoordinates predecessor;
            bool reachable = false;
            QueueCoordinates currentTileBeingChecked = new QueueCoordinates(currentCoordinates);
            currentTileBeingChecked.Distance = 0;
            queue.Enqueue(currentTileBeingChecked);

            while (queue.Count > 0)
            {
                currentTileBeingChecked = queue.Dequeue();

                if (!CollectionContainsQueueCoordinates(visitedCoordinates, currentTileBeingChecked.Coords))
                {
                    visitedCoordinates.Add(currentTileBeingChecked);

                }

                neighbors = Neighbors(currentTileBeingChecked.Coords);
                predecessor = currentTileBeingChecked;

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.X == target.X && neighbor.Y == target.Y)
                    {
                        if (grid[currentTileBeingChecked.Coords.Y][currentTileBeingChecked.Coords.X] == 'B' ||
                            grid[currentTileBeingChecked.Coords.Y][currentTileBeingChecked.Coords.X] == 'C')
                        {
                            continue;
                        }

                        QueueCoordinates qCoords = new QueueCoordinates(neighbor);
                        qCoords.Distance = predecessor.Distance + 1;
                        predecessors.Add(qCoords, predecessor);
                        currentTileBeingChecked = qCoords;
                        reachable = true;
                        break;
                    }

                    if (grid[neighbor.Y][neighbor.X] == 'B')
                    {
                        Bomb responsibleBombForExplosion = FindBombResponisbleForExplosion(bombList, new Coordinates(neighbor.X, neighbor.Y));
                        int distanceToTile = Math.Abs(currentCoordinates.X - neighbor.X) + Math.Abs(currentCoordinates.Y - neighbor.Y);
                        int turnsUntilTileExplodes = responsibleBombForExplosion.TurnsUntilExplode - distanceToTile;

                        if (turnsUntilTileExplodes < 2)
                        {
                            continue;
                        }
                    }
                    else if (grid[neighbor.Y][neighbor.X] != '.')
                    {
                        continue;
                    }

                    if (!CollectionContainsQueueCoordinates(visitedCoordinates, neighbor) && !QueueContainsCoordinates(queue, neighbor))
                    {
                        QueueCoordinates qCoords = new QueueCoordinates(neighbor);
                        qCoords.Distance = predecessor.Distance + 1;
                        predecessors.Add(qCoords, predecessor);
                        queue.Enqueue(qCoords);
                    }
                }

                if (reachable)
                {
                    break;
                }
            }

            List<Coordinates> path = new List<Coordinates>();

            if (!reachable ||
            grid[currentTileBeingChecked.Coords.Y][currentTileBeingChecked.Coords.X] == 'B' ||
            grid[currentTileBeingChecked.Coords.Y][currentTileBeingChecked.Coords.X] == 'C')
            {
                return path;

            }

            while (true)
            {
                path.Add(currentTileBeingChecked.Coords);
                if (currentTileBeingChecked.Coords.X == currentCoordinates.X && currentTileBeingChecked.Coords.Y == currentCoordinates.Y) break;
                currentTileBeingChecked = predecessors[currentTileBeingChecked];
            }

            path.Reverse();

            return path;
        }

        static void OrganizeTargetsIntoLists(List<Coordinates> quadTargetList, List<Coordinates> trioTargetList, List<Coordinates> duoTargetList, List<Tile> allTiles)
        {
            foreach (var tile in allTiles)
            {
                int amountOfTargets = tile.VisibleBoxes.Count;

                switch (amountOfTargets)
                {
                    case 4:
                        quadTargetList.Add(tile.Coords);
                        break;
                    case 3:
                        trioTargetList.Add(tile.Coords);
                        break;
                    case 2:
                        duoTargetList.Add(tile.Coords);
                        break;
                }
            }
        }

        static void UpdateAllTilesListWithVisibleBoxes(List<Tile> allTiles, List<string> grid, int explosionRadius, List<Coordinates> bombedTargets, List<Item> itemList)
        {
            foreach (var tile in allTiles)
            {
                //right side of tile
                if (tile.Coords.X < Row)
                {
                    for (var i = tile.Coords.X + 1; i < tile.Coords.X + explosionRadius; i++)
                    {
                        Coordinates potentialBox = new Coordinates(i, tile.Coords.Y);
                        if (i > Row)
                        {
                            break;
                        }

                        if (grid[tile.Coords.Y][i] == '.')
                        {
                            if (ItemListContainsCoordinates(itemList, potentialBox))
                            {
                                break;
                            }
                        }
                        else if (grid[tile.Coords.Y][i] == '1' ||
                            grid[tile.Coords.Y][i] == '2' ||
                            grid[tile.Coords.Y][i] == '0')
                        {
                            if (!CollectionContainsCoordinates(bombedTargets, potentialBox))
                            {
                                tile.VisibleBoxes.Add(potentialBox);
                            }
                            break;
                        }
                        else if (grid[tile.Coords.Y][i] == 'X' ||
                                 grid[tile.Coords.Y][i] == 'C')
                        {
                            break;
                        }
                    }
                }
                //left side of tile
                if (tile.Coords.X > 0)
                {
                    for (var i = tile.Coords.X - 1; i > tile.Coords.X - explosionRadius; i--)
                    {
                        Coordinates potentialBox = new Coordinates(i, tile.Coords.Y);
                        if (i < 0)
                        {
                            break;
                        }

                        if (grid[tile.Coords.Y][i] == '.')
                        {
                            if (ItemListContainsCoordinates(itemList, potentialBox))
                            {
                                break;
                            }
                        }
                        else if (grid[tile.Coords.Y][i] == '1' ||
                            grid[tile.Coords.Y][i] == '2' ||
                            grid[tile.Coords.Y][i] == '0')
                        {
                            if (!CollectionContainsCoordinates(bombedTargets, potentialBox))
                            {
                                tile.VisibleBoxes.Add(potentialBox);
                            }
                            break;
                        }
                        else if (grid[tile.Coords.Y][i] == 'X' ||
                                 grid[tile.Coords.Y][i] == 'C')
                        {
                            break;
                        }
                    }
                }
                //bottom of tile
                if (tile.Coords.Y < Col)
                {
                    for (var i = tile.Coords.Y + 1; i < tile.Coords.Y + explosionRadius; i++)
                    {
                        Coordinates potentialBox = new Coordinates(tile.Coords.X, i);
                        if (i > Col)
                        {
                            break;
                        }

                        if (grid[i][tile.Coords.X] == '.')
                        {
                            if (ItemListContainsCoordinates(itemList, potentialBox))
                            {
                                break;
                            }
                        }
                        else if (grid[i][tile.Coords.X] == '0' ||
                            grid[i][tile.Coords.X] == '1' ||
                            grid[i][tile.Coords.X] == '2')
                        {
                            if (!CollectionContainsCoordinates(bombedTargets, potentialBox))
                            {
                                tile.VisibleBoxes.Add(potentialBox);
                            }
                            break;
                        }
                        else if (grid[i][tile.Coords.X] == 'X' ||
                                 grid[i][tile.Coords.X] == 'C')
                        {
                            break;
                        }
                    }
                }
                //top of tile
                if (tile.Coords.Y > 0)
                {
                    for (var i = tile.Coords.Y - 1; i > tile.Coords.Y - explosionRadius; i--)
                    {
                        Coordinates potentialBox = new Coordinates(tile.Coords.X, i);
                        if (i < 0)
                        {
                            break;
                        }

                        if (grid[i][tile.Coords.X] == '.')
                        {
                            if (ItemListContainsCoordinates(itemList, potentialBox))
                            {
                                break;
                            }
                        }
                        else if (grid[i][tile.Coords.X] == '0' ||
                            grid[i][tile.Coords.X] == '1' ||
                            grid[i][tile.Coords.X] == '2')
                        {
                            if (!CollectionContainsCoordinates(bombedTargets, potentialBox))
                            {
                                tile.VisibleBoxes.Add(potentialBox);
                            }
                            break;
                        }
                        else if (grid[i][tile.Coords.X] == 'X' ||
                                 grid[i][tile.Coords.X] == 'C')
                        {
                            break;
                        }
                    }
                }
            }
        }

        static List<Tile> FindAllReachableTiles(Coordinates currentCoordinates, List<string> grid, List<Bomb> bombList)
        {
            List<Tile> reachableTiles = new List<Tile> { new Tile(currentCoordinates) };
            Queue<QueueCoordinates> queue = new Queue<QueueCoordinates>();
            List<QueueCoordinates> visitedCoordinates = new List<QueueCoordinates>();
            Dictionary<QueueCoordinates, QueueCoordinates> predecessors = new Dictionary<QueueCoordinates, QueueCoordinates>();
            QueueCoordinates predecessor;
            List<Coordinates> neighbors;
            QueueCoordinates currentTileBeingChecked = new QueueCoordinates(currentCoordinates);
            currentTileBeingChecked.Distance = 0;
            queue.Enqueue(currentTileBeingChecked);

            while (queue.Count > 0)
            {
                currentTileBeingChecked = queue.Dequeue();

                if (!CollectionContainsQueueCoordinates(visitedCoordinates, currentTileBeingChecked.Coords))
                {
                    visitedCoordinates.Add(currentTileBeingChecked);

                }

                neighbors = Neighbors(currentTileBeingChecked.Coords);
                predecessor = currentTileBeingChecked;

                foreach (var neighbor in neighbors.Where(neighbor => grid[neighbor.Y][neighbor.X] == '.' || grid[neighbor.Y][neighbor.X] == 'B'))
                {
                    if (grid[neighbor.Y][neighbor.X] == 'B')
                    {
                        Bomb responsibleBombForExplosion = FindBombResponisbleForExplosion(bombList, new Coordinates(neighbor.X, neighbor.Y));
                        int distanceToTile = Math.Abs(currentCoordinates.X - neighbor.X) + Math.Abs(currentCoordinates.Y - neighbor.Y);
                        int turnsUntilTileExplodes = responsibleBombForExplosion.TurnsUntilExplode - distanceToTile;

                        if (turnsUntilTileExplodes < 2)
                        {
                            continue;
                        }
                    }

                    if (!CollectionContainsQueueCoordinates(visitedCoordinates, neighbor) && !QueueContainsCoordinates(queue, neighbor))
                    {
                        QueueCoordinates qCoords = new QueueCoordinates(neighbor);
                        qCoords.Distance = predecessor.Distance + 1;
                        predecessors.Add(qCoords, predecessor);
                        queue.Enqueue(qCoords);
                        reachableTiles.Add(new Tile(neighbor));
                    }
                }

            }

            return reachableTiles;
        }

        static Coordinates LastTileToExplode(Coordinates currentCoordinates, List<string> grid, List<Bomb> bombList)
        {
            Queue<QueueCoordinates> queue = new Queue<QueueCoordinates>();
            List<QueueCoordinates> visitedCoordinates = new List<QueueCoordinates>();
            Dictionary<QueueCoordinates, QueueCoordinates> predecessors = new Dictionary<QueueCoordinates, QueueCoordinates>();
            QueueCoordinates predecessor;
            List<Coordinates> neighbors;
            QueueCoordinates currentTileBeingChecked = new QueueCoordinates(currentCoordinates);
            currentTileBeingChecked.Distance = 0;
            Coordinates lastToExplode = new Coordinates(20, 20);
            int turnsUntilExplosion = FindBombResponisbleForExplosion(bombList, currentCoordinates).TurnsUntilExplode;
            int lastToExplodeSmallestNeighborsTimer = 10;
            int currentTilesSmallestNeighborsTimer = 10;
            queue.Enqueue(currentTileBeingChecked);

            while (queue.Count > 0)
            {
                currentTileBeingChecked = queue.Dequeue();

                if (!CollectionContainsQueueCoordinates(visitedCoordinates, currentTileBeingChecked.Coords))
                {
                    visitedCoordinates.Add(currentTileBeingChecked);

                }

                neighbors = Neighbors(currentTileBeingChecked.Coords);
                predecessor = currentTileBeingChecked;

                foreach (var neighbor in neighbors)
                {
                    if (grid[neighbor.Y][neighbor.X] == 'B')
                    {
                        Bomb responsibleBombForExplosion = FindBombResponisbleForExplosion(bombList, new Coordinates(neighbor.X, neighbor.Y));
                        int distanceToTile = Math.Abs(currentCoordinates.X - neighbor.X) + Math.Abs(currentCoordinates.Y - neighbor.Y);

                        if (responsibleBombForExplosion.TurnsUntilExplode < currentTilesSmallestNeighborsTimer)
                        {
                            currentTilesSmallestNeighborsTimer = responsibleBombForExplosion.TurnsUntilExplode;
                        }

                        if (!CollectionContainsQueueCoordinates(visitedCoordinates, neighbor) && !QueueContainsCoordinates(queue, neighbor))
                        {
                            QueueCoordinates qCoords = new QueueCoordinates(neighbor);
                            qCoords.Distance = predecessor.Distance + 1;
                            predecessors.Add(qCoords, predecessor);
                            queue.Enqueue(qCoords);
                        }
                    }
                }

                if (turnsUntilExplosion <= FindBombResponisbleForExplosion(bombList, currentTileBeingChecked.Coords).TurnsUntilExplode &&
                    currentTilesSmallestNeighborsTimer < lastToExplodeSmallestNeighborsTimer &&
                    FindBombResponisbleForExplosion(bombList, currentTileBeingChecked.Coords).TurnsUntilExplode - currentTilesSmallestNeighborsTimer >= 3)
                {
                    lastToExplode = currentTileBeingChecked.Coords;
                    lastToExplodeSmallestNeighborsTimer = currentTilesSmallestNeighborsTimer;
                }
            }

            return lastToExplode;
        }

        static bool CollectionContainsQueueCoordinates(ICollection<QueueCoordinates> collection, Coordinates coordsToCheck)
        {
            return collection.Any(queueCoords => queueCoords.Coords.X == coordsToCheck.X && queueCoords.Coords.Y == coordsToCheck.Y);
        }

        static bool CollectionContainsCoordinates(ICollection<Coordinates> collection, Coordinates coordsToCheck)
        {
            return collection.Any(coords => coords.X == coordsToCheck.X && coords.Y == coordsToCheck.Y);
        }

        static bool ItemListContainsCoordinates(ICollection<Item> collection, Coordinates coordsToCheck)
        {
            return collection.Any(coords => coords.Coords.X == coordsToCheck.X && coords.Coords.Y == coordsToCheck.Y);
        }

        static bool QueueContainsCoordinates(Queue<QueueCoordinates> queue, Coordinates coordsToCheck)
        {
            return queue.Any(queueCoords => queueCoords.Coords.X == coordsToCheck.X && queueCoords.Coords.Y == coordsToCheck.Y);
        }

        static List<Coordinates> Neighbors(Coordinates tile)
        {
            List<Coordinates> neighbors = new List<Coordinates>();

            for (var i = tile.Y - 1; i <= tile.Y + 1; i += 2)
            {
                if (i >= 0 && i <= Col)
                {
                    neighbors.Add(new Coordinates(tile.X, i));
                }
            }

            for (var i = tile.X - 1; i <= tile.X + 1; i += 2)
            {
                if (i >= 0 && i <= Row)
                {
                    neighbors.Add(new Coordinates(i, tile.Y));
                }
            }

            return neighbors;
        }


        static void PopulateTargetListWithSingleBoxTargets(string rowString, int i, List<string> singlePatterns, List<Coordinates> targetList, List<Coordinates> bombedTargets)
        {
            foreach (var pat in singlePatterns)
            {
                List<int> foundIndexes = AllIndexesOf(rowString, pat);

                foreach (var index in foundIndexes)
                {
                    Coordinates addableTargets = new Coordinates(index, i);
                    if (!CollectionContainsCoordinates(bombedTargets, addableTargets))
                    {
                        targetList.Add(new Coordinates(index, i));
                    }
                }
            }
        }

        public static List<int> AllIndexesOf(this string str, string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException("the string to find may not be empty", "value");
            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length)
            {
                if (index > str.Length)
                {
                    return indexes;
                }
                index = str.IndexOf(value, index);
                if (index == -1)
                    return indexes;
                indexes.Add(index);
                index++;
            }
        }

        static Item ClosestItem(List<Item> itemList, Coordinates currentCoordinates, int id)
        {
            Item closestItem = new Item(1, new Coordinates(20, 20));
            int closestDistanceToItem = 99;

            foreach (var item in itemList)
            {
                if (item.Id == id)
                {
                    int distanceToItem = Math.Abs(currentCoordinates.X - item.Coords.X) + Math.Abs(currentCoordinates.Y - item.Coords.Y);

                    if (distanceToItem < closestDistanceToItem)
                    {
                        closestItem = item;
                        closestDistanceToItem = distanceToItem;
                    }
                }
            }

            return closestItem;
        }

        static bool IsItemInBombsPath(List<Bomb> bombList, Item item)
        {
            return bombList.SelectMany(bomb => bomb.AffectedCoords).Any(coords => item.Coords.X == coords.X && item.Coords.Y == coords.Y);
        }

        static Coordinates ClosestTarget(List<Coordinates> targetList, Coordinates currentCoordinates, List<string> grid, List<Bomb> bombList)
        {
            Coordinates closestCoords = new Coordinates(0, 0);
            int closestDistance = 99;

            foreach (var coords in targetList)
            {
                int distance = BfsPathfinder(currentCoordinates, coords, grid, bombList).Count;

                if (distance < closestDistance)
                {
                    closestCoords = coords;
                    closestDistance = distance;
                }
            }

            return closestCoords;
        }

        static Target SelectTarget(List<Coordinates> quadTargetList, List<Coordinates> trioTargetList, List<Coordinates> duoTargetList, List<Coordinates> singleTargetList,
                                        Coordinates currentCoordinates, List<string> grid, List<Bomb> bombList)
        {
            Coordinates target;

            if (quadTargetList.Count > 0)
            {
                target = ClosestTarget(quadTargetList, currentCoordinates, grid, bombList);
                List<Coordinates> path = BfsPathfinder(currentCoordinates, target, grid, bombList);
                if (path.Count <= 25)
                {
                    RemoveCoordsFromList(quadTargetList, target);
                    return new Target(4, target);
                }

            }

            if (trioTargetList.Count > 0)
            {
                target = ClosestTarget(trioTargetList, currentCoordinates, grid, bombList);
                List<Coordinates> path = BfsPathfinder(currentCoordinates, target, grid, bombList);
                if (path.Count <= 15)
                {
                    RemoveCoordsFromList(trioTargetList, target);
                    return new Target(3, target);
                }
            }

            if (duoTargetList.Count > 0)
            {
                target = ClosestTarget(duoTargetList, currentCoordinates, grid, bombList);
                List<Coordinates> path = BfsPathfinder(currentCoordinates, target, grid, bombList);
                if (path.Count <= 10)
                {
                    RemoveCoordsFromList(duoTargetList, target);
                    return new Target(2, target);
                }
            }

            target = ClosestTarget(singleTargetList, currentCoordinates, grid, bombList);
            RemoveCoordsFromList(singleTargetList, target);


            return new Target(1, target);
        }
        static void RemoveCoordsFromList(List<Coordinates> list, Coordinates coordsToRemove)
        {
            list.RemoveAt(FindIndex(list, coordsToRemove));
        }

        static void RemoveCoordsFromItemList(List<Item> list, Coordinates coordsToRemove)
        {
            list.RemoveAt(FindIndexInItemList(list, coordsToRemove));
        }

        static int FindIndexInItemList(List<Item> list, Coordinates coordsToFind)
        {
            int res = -1;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Coords.X == coordsToFind.X && list[i].Coords.Y == coordsToFind.Y)
                {
                    res = i;
                }
            }

            return res;
        }

        static int FindIndex(List<Coordinates> list, Coordinates coordsToFind)
        {
            int res = -1;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].X == coordsToFind.X && list[i].Y == coordsToFind.Y)
                {
                    res = i;
                }
            }

            return res;
        }

        static Bomb FindBombResponisbleForExplosion(List<Bomb> bombList, Coordinates explosionCoords)
        {
            Bomb responsibleBomb = null;
            int turnsUntilExplosion = 9;

            foreach (var bomb in bombList)
            {
                if (bomb.AffectedCoords.Where(coords => coords.X == explosionCoords.X && coords.Y == explosionCoords.Y).Any(coords => turnsUntilExplosion > bomb.TurnsUntilExplode))
                {
                    responsibleBomb = bomb;
                    turnsUntilExplosion = bomb.TurnsUntilExplode;
                }
            }

            return responsibleBomb;
        }

        static List<Coordinates> IsNextToBox(Coordinates currentCoordinates, List<string> grid, List<Coordinates> bombedTargets)
        {
            List<Coordinates> boxesNextToPlayer = new List<Coordinates>();

            int x = currentCoordinates.X - 1;
            int y = currentCoordinates.Y;

            for (var i = 0; i < 3; i++)
            {
                if (x >= 0 && x <= Row)
                {
                    Coordinates possibleBoxLocation = new Coordinates(x, y);
                    if (grid[y][x] == '0' || grid[y][x] == '1' || grid[y][x] == '2')
                    {
                        if (!CollectionContainsCoordinates(bombedTargets, possibleBoxLocation))
                        {
                            boxesNextToPlayer.Add(possibleBoxLocation);
                        }
                    }
                }

                x++;
            }

            x = currentCoordinates.X;
            y = currentCoordinates.Y - 1;

            for (var i = 0; i < 3; i++)
            {
                if (y >= 0 && y <= Col)
                {
                    Coordinates possibleBoxLocation = new Coordinates(x, y);
                    if (grid[y][x] == '0' || grid[y][x] == '1' || grid[y][x] == '2')
                    {
                        if (!CollectionContainsCoordinates(bombedTargets, possibleBoxLocation))
                        {
                            boxesNextToPlayer.Add(possibleBoxLocation);
                        }
                    }
                }

                y++;
            }

            return boxesNextToPlayer;
        }

        static void UpdatesBombListAndBombedTargets(List<Bomb> bombList, List<Coordinates> bombedTargets, List<Item> itemList, List<string> grid)
        {
            //Updates Grid with bomb explo radius
            //Updates itemList, removing items in path of bombs
            //Updates bombedTargets, removing targets being blown up
            //Updates bombs, if they reach a different bomb, calculates chain explo effects
            foreach (var bomb in bombList)
            {
                //right side of bomb
                if (bomb.CenterCoords.X < Row)
                {
                    for (var a = bomb.CenterCoords.X + 1; a < bomb.CenterCoords.X + bomb.ExplosionRadius; a++)
                    {
                        Coordinates coords = new Coordinates(a, bomb.CenterCoords.Y);
                        if (a > Row)
                        {
                            break;
                        }

                        if (grid[bomb.CenterCoords.Y][a] == '.' || grid[bomb.CenterCoords.Y][a] == 'B')
                        {
                            //Items appear as '.'
                            //if item is in the bombs path
                            //stops the bomb at that point and removes item from itemlist
                            if (ItemListContainsCoordinates(itemList, coords))
                            {
                                RemoveCoordsFromItemList(itemList, coords);
                                grid[bomb.CenterCoords.Y] = grid[bomb.CenterCoords.Y].Substring(0, a) + 'B' + grid[bomb.CenterCoords.Y].Substring(a + 1);
                                bomb.AffectedCoords.Add(coords);
                                break;
                            }
                            //otherwise its not an item, keeps going
                            else
                            {
                                grid[bomb.CenterCoords.Y] = grid[bomb.CenterCoords.Y].Substring(0, a) + 'B' + grid[bomb.CenterCoords.Y].Substring(a + 1);
                                bomb.AffectedCoords.Add(coords);
                            }
                        }
                        else
                        {
                            //if bomb explo reaches a different bombs center
                            //recalculates explosion time, changing it to the smaller one
                            if (grid[bomb.CenterCoords.Y][a] == 'C')
                            {
                                //Bomb coords is the current one being looked at
                                //Gets the bomb whos center is found
                                //&& to ||
                                Bomb chainBomb = bombList.FirstOrDefault(x => x.CenterCoords.X == a && x.CenterCoords.Y == bomb.CenterCoords.Y);
                                //if current bomb has a smaller timer than found bomb
                                // changes found bombs' timer to the smaller one
                                if (bomb.TurnsUntilExplode < chainBomb.TurnsUntilExplode)
                                {
                                    chainBomb.TurnsUntilExplode = bomb.TurnsUntilExplode;
                                }
                            }
                            //else checks if its a box and adds those boxes to bombedTargets
                            // otherwise stops at 'X'
                            else if (grid[bomb.CenterCoords.Y][a] == '1' ||
                                grid[bomb.CenterCoords.Y][a] == '2' ||
                                grid[bomb.CenterCoords.Y][a] == '0')
                            {
                                if (!CollectionContainsCoordinates(bombedTargets, coords))
                                {
                                    bombedTargets.Add(coords);
                                }
                            }
                            break;
                        }
                    }
                }
                //left side of bomb
                if (bomb.CenterCoords.X > 0)
                {
                    for (var a = bomb.CenterCoords.X - 1; a > bomb.CenterCoords.X - bomb.ExplosionRadius; a--)
                    {
                        Coordinates coords = new Coordinates(a, bomb.CenterCoords.Y);
                        if (a < 0)
                        {
                            break;
                        }

                        if (grid[bomb.CenterCoords.Y][a] == '.' || grid[bomb.CenterCoords.Y][a] == 'B')
                        {
                            if (ItemListContainsCoordinates(itemList, coords))
                            {
                                RemoveCoordsFromItemList(itemList, coords);
                                grid[bomb.CenterCoords.Y] = grid[bomb.CenterCoords.Y].Substring(0, a) + 'B' + grid[bomb.CenterCoords.Y].Substring(a + 1);
                                bomb.AffectedCoords.Add(coords);
                                break;
                            }
                            else
                            {
                                grid[bomb.CenterCoords.Y] = grid[bomb.CenterCoords.Y].Substring(0, a) + 'B' + grid[bomb.CenterCoords.Y].Substring(a + 1);
                                bomb.AffectedCoords.Add(coords);
                            }
                        }
                        else
                        {
                            if (grid[bomb.CenterCoords.Y][a] == 'C')
                            {
                                //Bomb coords is the current one being looked at
                                //Gets the bomb whos center is found
                                Bomb chainBomb = bombList.FirstOrDefault(x => x.CenterCoords.X == a && x.CenterCoords.Y == bomb.CenterCoords.Y);
                                //if current bomb has a smaller timer than found bomb
                                // changes found bombs' timer to the smaller one
                                if (bomb.TurnsUntilExplode < chainBomb.TurnsUntilExplode)
                                {
                                    chainBomb.TurnsUntilExplode = bomb.TurnsUntilExplode;
                                }
                            }
                            //else checks if its a box and adds those boxes to bombedTargets
                            // otherwise stops at 'X'
                            else if (grid[bomb.CenterCoords.Y][a] == '1' ||
                                grid[bomb.CenterCoords.Y][a] == '2' ||
                                grid[bomb.CenterCoords.Y][a] == '0')
                            {
                                if (!CollectionContainsCoordinates(bombedTargets, coords))
                                {
                                    bombedTargets.Add(coords);
                                }
                            }
                            break;
                        }
                    }
                }
                //bottom of the bomb
                if (bomb.CenterCoords.Y < Col)
                {
                    for (var b = bomb.CenterCoords.Y + 1; b < bomb.CenterCoords.Y + bomb.ExplosionRadius; b++)
                    {
                        Coordinates coords = new Coordinates(bomb.CenterCoords.X, b);
                        if (b > Col)
                        {
                            break;
                        }

                        if (grid[b][bomb.CenterCoords.X] == '.' || grid[b][bomb.CenterCoords.X] == 'B')
                        {
                            if (ItemListContainsCoordinates(itemList, coords))
                            {
                                RemoveCoordsFromItemList(itemList, coords);
                                grid[b] = grid[b].Substring(0, bomb.CenterCoords.X) + 'B' + grid[b].Substring(bomb.CenterCoords.X + 1);
                                bomb.AffectedCoords.Add(coords);
                                break;
                            }
                            else
                            {
                                grid[b] = grid[b].Substring(0, bomb.CenterCoords.X) + 'B' + grid[b].Substring(bomb.CenterCoords.X + 1);
                                bomb.AffectedCoords.Add(coords);
                            }
                        }
                        else
                        {
                            if (grid[b][bomb.CenterCoords.X] == 'C')
                            {
                                //Bomb coords is the current one being looked at
                                //Gets the bomb whos center is found
                                Bomb chainBomb = bombList.FirstOrDefault(x => x.CenterCoords.X == bomb.CenterCoords.X && x.CenterCoords.Y == b);
                                //if current bomb has a smaller timer than found bomb
                                // changes found bombs' timer to the smaller one
                                if (bomb.TurnsUntilExplode < chainBomb.TurnsUntilExplode)
                                {
                                    chainBomb.TurnsUntilExplode = bomb.TurnsUntilExplode;
                                }
                            }
                            //else checks if its a box and adds those boxes to bombedTargets
                            // otherwise stops at 'X'
                            else if (grid[b][bomb.CenterCoords.X] == '1' ||
                                grid[b][bomb.CenterCoords.X] == '2' ||
                                grid[b][bomb.CenterCoords.X] == '0')
                            {
                                if (!CollectionContainsCoordinates(bombedTargets, coords))
                                {
                                    bombedTargets.Add(coords);
                                }
                            }
                            break;
                        }
                    }
                }
                //top of the bomb
                if (bomb.CenterCoords.Y > 0)
                {
                    for (var b = bomb.CenterCoords.Y - 1; b > bomb.CenterCoords.Y - bomb.ExplosionRadius; b--)
                    {
                        Coordinates coords = new Coordinates(bomb.CenterCoords.X, b);
                        if (b < 0)
                        {
                            break;
                        }

                        if (grid[b][bomb.CenterCoords.X] == '.' || grid[b][bomb.CenterCoords.X] == 'B')
                        {
                            if (ItemListContainsCoordinates(itemList, coords))
                            {
                                RemoveCoordsFromItemList(itemList, coords);
                                grid[b] = grid[b].Substring(0, bomb.CenterCoords.X) + 'B' + grid[b].Substring(bomb.CenterCoords.X + 1);
                                bomb.AffectedCoords.Add(coords);
                                break;
                            }
                            else
                            {
                                grid[b] = grid[b].Substring(0, bomb.CenterCoords.X) + 'B' + grid[b].Substring(bomb.CenterCoords.X + 1);
                                bomb.AffectedCoords.Add(coords);
                            }
                        }
                        else
                        {
                            if (grid[b][bomb.CenterCoords.X] == 'C')
                            {
                                //Bomb coords is the current one being looked at
                                //Gets the bomb whos center is found
                                Bomb chainBomb = bombList.FirstOrDefault(x => x.CenterCoords.X == bomb.CenterCoords.X && x.CenterCoords.Y == b);
                                //if current bomb has a smaller timer than found bomb
                                // changes found bombs' timer to the smaller one
                                if (bomb.TurnsUntilExplode < chainBomb.TurnsUntilExplode)
                                {
                                    chainBomb.TurnsUntilExplode = bomb.TurnsUntilExplode;
                                }
                            }
                            //else checks if its a box and adds those boxes to bombedTargets
                            // otherwise stops at 'X'
                            else if (grid[b][bomb.CenterCoords.X] == '1' ||
                                grid[b][bomb.CenterCoords.X] == '2' ||
                                grid[b][bomb.CenterCoords.X] == '0')
                            {
                                if (!CollectionContainsCoordinates(bombedTargets, coords))
                                {
                                    bombedTargets.Add(coords);
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }

        static void CopyCoordinatesList(List<Coordinates> copyList, List<Coordinates> originalList)
        {
            copyList.AddRange(originalList.Select(item => new Coordinates(item.X, item.Y)));
        }

        static void CopyItemList(List<Item> copyList, List<Item> originalList)
        {
            copyList.AddRange(originalList.Select(item => new Item(item.Id, new Coordinates(item.Coords.X, item.Coords.Y))));
        }

        static void CopyBombList(List<Bomb> copyList, List<Bomb> originalList)
        {
            foreach (var item in originalList)
            {
                Bomb bomb = new Bomb(item.CenterCoords, item.TurnsUntilExplode, item.ExplosionRadius);
                CopyCoordinatesList(bomb.AffectedCoords, item.AffectedCoords);
                copyList.Add(bomb);
            }
        }

        static (List<Coordinates>, bool, Coordinates) PotentialSituation(List<string> grid, Target target, List<Bomb> bombList, List<Item> itemList, List<Coordinates> bombedTargets, int currentExplosionRadius)
        {
            //Creates potential variables, otherwise shallow copy

            //Creates a potentialGrid, that will be altered to check if bomb was placed at target
            List<string> potentialGrid = new List<string>(grid);
            Coordinates potentialCoordinates = target.Coords;
            //Creates a potentialBombList (will add the next bomb to it)
            List<Bomb> potentialBombList = new List<Bomb>();
            CopyBombList(potentialBombList, bombList);
            potentialBombList.Add(new Bomb(target.Coords, 8, currentExplosionRadius));
            //Creates a potentialItemList
            List<Item> potentialItemList = new List<Item>();
            CopyItemList(potentialItemList, itemList);
            //Creates a potentialBombedTargetList
            List<Coordinates> potentialBombedTargets = new List<Coordinates>();
            CopyCoordinatesList(potentialBombedTargets, bombedTargets);
            //Edits the simulated bomb into potentialGrid
            potentialGrid[target.Coords.Y] = potentialGrid[target.Coords.Y].Substring(0, target.Coords.X) + 'C' + potentialGrid[target.Coords.Y].Substring(target.Coords.X + 1);
            UpdatesBombListAndBombedTargets(potentialBombList, potentialBombedTargets, potentialItemList, potentialGrid);
            //returns potential path to safety
            Coordinates lastToExplode = LastTileToExplode(potentialCoordinates, potentialGrid, potentialBombList);
            return (MoveToSafety(potentialCoordinates, potentialGrid, potentialBombList), lastToExplode.X == 20 && lastToExplode.Y == 20, lastToExplode);
        }

        static int ClosestPlayer(Coordinates currentCoordinates, List<PlayerEntity> allPlayers, int myId)
        {
            int closestDistance = 99;

            foreach (var player in allPlayers)
            {
                //skip the player
                //always will be the closest
                if (player.Id == myId)
                {
                    continue;
                }

                int distance = Math.Abs(currentCoordinates.X - player.Coordinates.X) + Math.Abs(currentCoordinates.Y - player.Coordinates.Y);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }

            return closestDistance;
        }

        static bool FreeLineToTarget(List<string> grid, Coordinates currentCoordinates, Target target, List<Item> itemList)
        {
            bool flag = true;
            int smallestCoordinate;
            int largestCoordinate;

            if (currentCoordinates.X == target.Coords.X)
            {
                if (currentCoordinates.Y > target.Coords.Y)
                {
                    smallestCoordinate = target.Coords.Y;
                    largestCoordinate = currentCoordinates.Y;
                }
                else
                {
                    smallestCoordinate = currentCoordinates.Y;
                    largestCoordinate = target.Coords.Y;
                }

                for (var i = smallestCoordinate; i < largestCoordinate; i++)
                {
                    if (grid[i][currentCoordinates.X] == 'X')
                    {
                        flag = false;
                        break;
                    }

                    if (ItemListContainsCoordinates(itemList, new Coordinates(smallestCoordinate, currentCoordinates.Y)))
                    {
                        flag = false;
                        break;
                    }
                }
            }
            else if (currentCoordinates.Y == target.Coords.Y)
            {
                if (currentCoordinates.X > target.Coords.X)
                {
                    smallestCoordinate = target.Coords.X;
                    largestCoordinate = currentCoordinates.X;
                }
                else
                {
                    smallestCoordinate = currentCoordinates.X;
                    largestCoordinate = target.Coords.X;
                }

                for (var i = smallestCoordinate; i < largestCoordinate; i++)
                {
                    if (grid[currentCoordinates.Y][i] == 'X')
                    {
                        flag = false;
                        break;
                    }

                    if (ItemListContainsCoordinates(itemList, new Coordinates(i, smallestCoordinate)))
                    {
                        flag = false;
                        break;
                    }
                }
            }

            return flag;
        }
    }
    class Coordinates
    {
        public int X;
        public int Y;

        public Coordinates(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    class QueueCoordinates
    {
        public Coordinates Coords;
        public int Distance;

        public QueueCoordinates(Coordinates coords)
        {
            Coords = coords;
        }
    }

    class Bomb
    {
        public Coordinates CenterCoords;
        public int TurnsUntilExplode;
        public int ExplosionRadius;
        public List<Coordinates> AffectedCoords;

        public Bomb(Coordinates center, int turnsUntilExplosion, int explosionRadius)
        {
            CenterCoords = center;
            TurnsUntilExplode = turnsUntilExplosion;
            ExplosionRadius = explosionRadius;
            AffectedCoords = new List<Coordinates> { CenterCoords };
        }
    }

    class Tile
    {
        public Coordinates Coords;
        public List<Coordinates> VisibleBoxes;

        public Tile(Coordinates coords)
        {
            Coords = coords;
            VisibleBoxes = new List<Coordinates>();
        }
    }

    class Item
    {
        public Coordinates Coords;
        public int Id;

        public Item(int id, Coordinates coords)
        {
            Id = id;
            Coords = coords;
        }
    }

    class Target
    {
        public int Value;
        public Coordinates Coords;

        public Target(int value, Coordinates coords)
        {
            Value = value;
            Coords = coords;
        }
    }

    class PlayerEntity
    {
        public Coordinates Coordinates;
        public int ExploRadius;
        public int Id;

        public PlayerEntity(Coordinates coords, int exploRadius, int id)
        {
            Coordinates = coords;
            ExploRadius = exploRadius;
            Id = id;
        }
    }
}