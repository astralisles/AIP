# bank

Accounts with I64 balances: alice 100, bob 50. A transaction is a variant: `Deposit(account, amount)`,
`Withdraw(account, amount)`, `Transfer(from, to, amount)`. Applying one gives either the new accounts or a
rejection with a reason (a variant value, never a crash). A rejected transaction changes nothing.
Rejections: `unknown account <name>` (checked first, `from` before `to`), then `insufficient funds`.

Apply these in order and print one line each, then the final balances in alphabetical order:

    Deposit bob 25, Withdraw alice 30, Transfer alice bob 50, Withdraw bob 500, Transfer bob carol 10, Deposit alice 5