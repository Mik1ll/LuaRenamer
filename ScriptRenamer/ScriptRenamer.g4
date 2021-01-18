// Define a grammar called Hello
grammar ScriptRenamer;
r : stmt* EOF;

stmt
    :   if_stmt
    |   add_stmt
    |   replace_stmt
    |   block
    ;

if_stmt
    :   'if' bool_expr block ('else' block)?
    ;

bool_expr
    :   bool_expr and='and' bool_expr
    |   bool_expr or='or' bool_expr
    |   '(' bool_expr ')'
    |   bool_atom
    ;

bool_atom
    :   BOOLEAN
    ;

add_stmt
    : 'add'
    ;

replace_stmt
    : 'replace'
    ;

block
    :   '{' stmt* '}'
    ;


// Tags



// Literals
NUMBER
    :   [+-]? INTEGER
    |   [+-]? FLOAT
    ;

fragment INTEGER
    :   DIGIT+
    ;

fragment DIGIT
    :   [0-9]
    ;

fragment FLOAT
    :   DIGIT+ '.' DIGIT*
    ;

BOOLEAN
    :   ([tT] 'rue' | [fF] 'alse')
    ;

STRING
    :   '\'' .*? '\''
    |   '"' .*? '"'
    ;

WS : [ \t\r\n]+ -> skip ; // skip spaces, tabs, newlines
UNICODE_WS : [\p{White_Space}] -> skip; // match all Unicode whitespace

BlockComment
    :   '/*' .*? '*/'
        -> skip
    ;

LineComment
    :   '//' ~[\r\n]*
        -> skip
    ;