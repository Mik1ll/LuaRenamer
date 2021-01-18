// Define a grammar called Hello
grammar ScriptRenamer;
start : stmt* EOF;

stmt
    :   if_stmt
    |   non_if_stmt
    ;

if_stmt
    :   IF bool_expr (non_if_stmt ELSE stmt | stmt)
    ;

non_if_stmt
    :   add_stmt
    |   replace_stmt
    |   block
    ;

bool_expr
    :   bool_expr op=AND bool_expr
    |   bool_expr op=OR bool_expr
    |   op=NOT bool_expr
    |   LPAREN bool_expr RPAREN
    |   bool_atom
    ;

bool_atom
    :   BOOLEAN
    |   bool_labels
    ;

bool_labels
    :   string_labels
    ;

add_stmt
    :   ADD string_atom+
    ;

string_atom
    :   STRING
    |   string_labels
    ;

string_labels   
    :   ANIMENAMEROMAJI
    |   ANIMENAMEENGLISH
    |   ANIMENAMEJAPANESE
    ;

replace_stmt
    : REPLACE
    ;

block
    :   LBRACK stmt* RBRACK
    ;


// Control Tokens
IF : 'if';
ELSE : 'else';
LBRACK : '{';
RBRACK : '}';

// Statement Tokens
ADD : 'add';
REPLACE : 'replace';

// Operators
AND : 'and';
OR : 'or';
NOT : 'not';
LPAREN : '(';
RPAREN : ')';

// Tags
    // Strings
ANIMENAMEROMAJI : 'AnimeNameRomaji';
ANIMENAMEENGLISH : 'AnimeNameEnglish';
ANIMENAMEJAPANESE : 'AnimeNameJapanese';
EPISODENAMEROMAJI : 'EpisodeNameRomaji';
EPISODENAMEENGLISH : 'EpisodeNameEnglish';
EPISODENAMEJAPANESE : 'EpisodeNameJapanese';
GROUPSHORT : 'GroupShort';
GROUPLONG : 'GroupLong';
CRCLOWER : 'CRCLower';
CRCUPPER : 'CRCUpper';
SOURCESHORT : 'SourceShort';
SOURCELONG : 'SourceLong';
RESOLUTION : 'Resolution';
ANIMETYPE : 'AnimeType';
VIDEOCODEC : 'VideoCodec';

    // Numbers
EPISODENUMBER : 'EpisodeNumber';
FILEVERSION : 'FileVersion';
WIDTH : 'Width';
HEIGHT : 'Height';
YEAR : 'Year';
EPISODECOUNT : 'EpisodeCount';
BITDEPTH : 'BitDepth';

    // Collections
AUDIOCODECS : 'AudioCodecs';
DUBLANGUAGES : 'DubLanguages';
SUBLANGUAGES : 'SubLanguages';







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
    :   ('true' | 'false')
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