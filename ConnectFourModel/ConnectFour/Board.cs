using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Model.ConnectFour
{
    public class Board
    {
        [Flags]
        public enum EventType
        {
            BoardUpdate = 1,
            TileAdded = 2,
            GameWon = 4,
        }

        public class EventFlag
        {
            public bool handled = true;
        }

        /// <summary>
        /// A tile event delegate indicating that a tile event has occurred at the given x/y coordinate.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="value"></param>
        public delegate void TileEvent(EventType type, int x, int y, int value);

        /// <summary>
        /// Called when the game board achieves a victory state.
        /// </summary>
        public event TileEvent? GameWon;

        /// <summary>
        /// Called when the board is changed. Generally, this is only called for board changes
        /// that do not trigger tile addition events.
        /// </summary>
        public event TileEvent? BoardUpdated;

        /// <summary>
        /// The internal width of the board
        /// </summary>
        public readonly int _width;
        /// <summary>
        /// The internal height of the board
        /// </summary>
        public readonly int _height;
        /// <summary>
        /// Gets the width of the board
        /// </summary>
        public int Width => _width;
        /// <summary>
        /// Gets the height of the board
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// The layout of the board
        /// </summary>
        int[] layout;

        /// <summary>
        /// Accesses the cell of this board referenced by the given coordinates
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int this[int x, int y]
        {
            get
            {
                //Bounds check that the x/y is within range
                if (!isContained(x, y))
                    throw new IndexOutOfRangeException($"Cell ({x},{y}) not in grid ({_width}x{_height}!");
                return layout[x * _height + y];
            }
            set
            {
                //Bounds check that the x/y is within range
                if (!isContained(x, y))
                    throw new IndexOutOfRangeException($"Cell ({x},{y}) not in grid ({_width}x{_height}!");
                layout[x * _height + y] = value;
            }
        }

        /// <summary>
        /// Returns a boolean determining if the given coordinate is contained
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool isContained(int x, int y) => x >= 0 && y >= 0 && x <= _width && y <= _height;

        /// <summary>
        /// Creates a board with the given width and height
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public Board(int width, int height)
        {
            if (width * height < 16)
                throw new ArgumentException("Width/Height is too small")!;

            _width = width;
            _height = height;

            layout = new int[width * height];

        }

        /// <summary>
        /// Tries to add a tile of the given value to the given column.
        /// </summary>
        /// <param name="column">The column to insert the tile</param>
        /// <param name="value">The value of the tile to insert</param>
        /// <returns></returns>
        public bool TryAddColumn(int column, int value)
        {
            //Check the bounds
            if (column < 0 || column >= _width) return false;
            //Check that the column has space
            if (this[column, 0] != 0) return false;

            //The column has space, so we can interate safely up from the bottom
            for (int i = _height; i >= 0; --i)
            {
                //we have now found the first empty space in the column
                if (this[column, i] == 0)
                {
                    this[column, i] = value;
                    BoardUpdated?.Invoke(EventType.BoardUpdate | EventType.TileAdded, column, i, value);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Validates the board
        /// </summary>
        /// <returns></returns>
        public int CheckBoard()
        {
            bool changed = false;
            for (int x = 0; x < _width; ++x)
            {
                for (int y = _height - 2; y >= 0; --y)
                {
                    //Ensure that all tiles are pushed down
                    if (this[x, y + 1] == 0 && this[x, y] != 0)
                    {
                        this[x, y + 1] = this[x, y];
                        this[x, y] = 0;
                        changed = true;
                    }

                }
            }
            //Notify that the board layout has changed
            if (changed)
            {
                BoardUpdated?.Invoke(EventType.BoardUpdate, -1, -1, -1);
            }

            //now check the board for 4x4 completions
            for (int x = 0; x < _width; ++x)
            {
                for (int y = 0; y <= _height; ++y)
                {
                    if (this[x, y] != 0)
                    {
                        //check every direction
                        for (int i = 0; i < 8; i++)
                        {
                            //now set up the point walking thingy
                            DirPoint di = new(x, y, i);
                            int counter = 0;
                            //and move in the ascribed direction until we arrive or bork
                            while (true)
                            {
                                //step
                                di = di.Step();
                                //check the destination is (1) in the grid
                                //and (2) matches the current value
                                if (isContained(di.X, di.Y) && this[di.X, di.Y] == this[x, y])
                                {
                                    //count the step and continue the counting
                                    ++counter;
                                    //Remember we only need 3 additional tiles for this to work
                                    if (counter >= 3)
                                    {
                                        //return the value
                                        return this[x, y];
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return 0;

        }




    }
}
