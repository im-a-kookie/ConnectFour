namespace ConnectFour.ConnectFour
{
    /// <summary>
    /// A directional point that allows simple directional stepping
    /// </summary>
    internal struct DirPoint
    {
        static int[][] dirs = [
            [-1, -1], [-1, 0], [-1, 1],
            [0,  -1],          [0,  1],
            [1,  -1], [1,  0], [1,  1]
        ];

        /// <summary>
        /// The X/Y/Dir of the point
        /// </summary>
        public int X, Y, Dir;
        /// <summary>
        /// Sets up a Dir point from the given coordinates, in the given direction
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="Dir"></param>
        public DirPoint(int x, int y, int Dir)
        {
            X = x; Y = y; this.Dir = Dir & 0x7;
        }

        /// <summary>
        /// Step this coordinate by one unit in the set direction
        /// </summary>
        public DirPoint Step()
        {
            return new(
                X + dirs[Dir][0],
                Y + dirs[Dir][1],
                Dir
            );
        }


    }
}
