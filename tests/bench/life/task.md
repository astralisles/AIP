# life

Conway's Game of Life on a 5x5 grid (cells outside the grid are dead). Start with a vertical blinker:
live cells at (row 1, col 2), (row 2, col 2), (row 3, col 2), rows and columns counted from 0.
Print generations 0, 1 and 2: a line `gen <n>`, then the 5 rows, `#` for live and `.` for dead.
The grid must be data (for example a list of rows) and each generation computed from the one before.