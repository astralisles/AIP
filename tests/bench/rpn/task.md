# rpn

Evaluate reverse-Polish expressions (space-separated tokens; integers and `+ - * /`, integer division).
For each expression below, in this order, print `<expression> = <result>`.
A failure must be an ordinary value (a variant case), never a crash; print `<expression> = error: <reason>`.

    3 4 +
    5 1 2 + 4 * + 3 -
    2 3 4 * +
    10 2 /
    1 +
    4 0 /
    2 x +

The expressions are Text values in the program; the tokens must be split and the numbers parsed at run time.