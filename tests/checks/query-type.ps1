# aip query: `type NAME` and `users TYPE` answer what uses a struct or variant (T-0965).
$t = AipOut query examples\aipc type FnHeader
Report 'query type: shows fields and users' ($t.Contains('struct FnHeader') -and $t.Contains('fields FnHeader(name Text') -and $t.Contains('used by: indexLine, parseConstHeader, parseFnHeader, parseHeader')) $t
$u = AipOut query examples\aipc users FnHeader
Report 'query users: says how each function uses the type' ($u.Contains('indexLine(h FnHeader) -> Text | pure | parameter, reads field') -and $u.Contains('parseFnHeader(toks List<Text>) -> FnHeader | pure | constructs, returns')) $u
$n = AipOut query examples\aipc users Nope
Report 'query users: unknown type is an error' ($n.Contains('no-such-type')) $n
