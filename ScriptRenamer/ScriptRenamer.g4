grammar ScriptRenamer;
// Rules
    start : ctrlstmt* EOF;
    
    // Statements
        
        ctrlstmt
            :   (ctrl | stmt)
            ;
        
        ctrl
            :   if_stmt
            |   block
            ;
        
        stmt
            :   (
                target_labels? op=ADD string_atom+
            |   target_labels? op=SET string_atom+
            |   target_labels? op=REPLACE string_atom string_atom
            |   cancel=CANCEL string_atom*
            |   cancel=(SKIPRENAME | SKIPMOVE)
            |   FINDLASTLOCATION
            |   REMOVERESERVEDCHARS
                ) SEMICOLON
            ;

        if_stmt
            :   IF LPAREN bool_expr RPAREN (true_branch=ctrlstmt | true_branch=ctrlstmt ELSE false_branch=ctrlstmt)
            ;

        block
            :   LBRACK ctrlstmt* RBRACK
            ;

    // Expressions
        bool_expr
            :   op=NOT bool_expr
            |   collection_expr
            |   is_left=ANIMETYPE op=IS ANIMETYPE_ENUM
            |   is_left=EPISODETYPE op=IS EPISODETYPE_ENUM
            |   string_atom op=CONTAINS string_atom
            |   number_atom op=(GT | GE | LT | LE) number_atom
            |   bool_expr op=(EQ | NE) bool_expr
            |   number_atom op=(EQ | NE) number_atom
            |   string_atom op=(EQ | NE) string_atom
            |   bool_expr op=AND bool_expr
            |   bool_expr op=OR bool_expr
            |   op=LPAREN bool_expr RPAREN
            |   bool_atom
            ;

        collection_expr
            :   collection_expr HAS string_atom
            |   collection_expr HAS (LANGUAGE_ENUM (AND TITLETYPE_ENUM)? | TITLETYPE_ENUM (AND LANGUAGE_ENUM)?)
            |   FIRST LPAREN collection_expr RPAREN
            |   collection_labels
            ;

    // Atoms
        bool_atom
            :   BOOLEAN
            |   bool_labels
            |   number_atom
            |   string_atom
            ;

        number_atom
            :   NUMBER
            |   number_labels
            |   LENGTH LPAREN (collection_expr | string_atom) RPAREN
            ;

        string_atom
            :   op=STRING
            |   string_labels
            |   collection_expr
            |   number_atom (op=PAD number_atom)?
            |   date_atom
            |   string_atom op=PLUS string_atom
            |   op=(REPLACE | RXREPLACE) LPAREN string_atom COMMA string_atom COMMA string_atom RPAREN
            |   op=SUBSTRING LPAREN string_atom COMMA number_atom (COMMA number_atom)? RPAREN
            |   op=TRUNCATE LPAREN string_atom COMMA number_atom RPAREN
            |   op=TRIM LPAREN string_atom RPAREN
            |   op=RXMATCH LPAREN string_atom COMMA string_atom RPAREN
            |   op=(UPPER | LOWER) LPAREN string_atom RPAREN
            ;

        date_atom
            :   type=(ANIMERELEASEDATE | EPISODERELEASEDATE | FILERELEASEDATE) (DOT field=(DAY | MONTH | YEAR))?
            ;

    // Labels
        number_labels
            :   label=(ANIMEID
            |   EPISODEID
            |   EPISODENUMBER
            |   VERSION
            |   WIDTH
            |   HEIGHT
            |   EPISODECOUNT
            |   BITDEPTH
            |   AUDIOCHANNELS
            |   SERIESINGROUP
            |   LASTEPISODENUMBER
            |   MAXEPISODECOUNT)
            ;

        bool_labels
            :   label=(RESTRICTED
            |   CENSORED
            |   CHAPTERED
            |   MANUALLYLINKED
            |   INDROPSOURCE
            |   MULTILINKED)
            ;

        collection_labels
            :   label=(AUDIOCODECS
            |   DUBLANGUAGES
            |   SUBLANGUAGES
            |   ANIMETITLES
            |   EPISODETITLES
            |   IMPORTFOLDERS)
            ;

        string_labels
            :   label=(ANIMETITLEPREFERRED
            |   ANIMETITLEROMAJI
            |   ANIMETITLEENGLISH
            |   ANIMETITLEJAPANESE
            |   EPISODETITLEROMAJI
            |   EPISODETITLEENGLISH
            |   EPISODETITLEJAPANESE
            |   GROUPSHORT
            |   GROUPLONG
            |   CRCLOWER
            |   CRCUPPER
            |   SOURCE
            |   RESOLUTION
            |   ANIMETYPE
            |   EPISODETYPE
            |   EPISODEPREFIX
            |   VIDEOCODECLONG
            |   VIDEOCODECSHORT
            |   DURATION
            |   GROUPNAME
            |   OLDFILENAME
            |   ORIGINALFILENAME
            |   OLDIMPORTFOLDER
            |   VIDEOCODECANIDB
            |   FILENAME
            |   DESTINATION
            |   SUBFOLDER)
            |   label=EPISODENUMBERS (PAD number_atom)?
            ;

        target_labels
            :   label=(FILENAME
            |   DESTINATION
            |   SUBFOLDER)
            ;

// Control Tokens
    IF : 'if';
    ELSE : 'else';
    LBRACK : '{';
    RBRACK : '}';

// Statement Tokens
    ADD : 'add';
    REPLACE : 'replace';
    SET : 'set';
    FILENAME : 'filename';
    DESTINATION : 'destination';
    SUBFOLDER : 'subfolder';
    CANCEL : 'cancel';
    SKIPRENAME : 'skiprename';
    SKIPMOVE : 'skipmove';
    FINDLASTLOCATION : 'findlastlocation';
    REMOVERESERVEDCHARS : 'removereservedchars';

// Operators
    AND : 'and';
    OR : 'or';
    NOT : 'not';
    LPAREN : '(';
    RPAREN : ')';
    HAS : 'has';
    LT : '<';
    GT : '>';
    LE : '<=';
    GE : '>=';
    EQ : '==';
    NE : '!=';
    IS : 'is';
    LENGTH : 'len';
    FIRST : 'first';
    DOT : '.';
    CONTAINS : 'contains';
    PAD : 'pad';
    PLUS : '+';
    COMMA : ',';
    SEMICOLON: ';';
    SUBSTRING : 'substr';
    TRUNCATE : 'trunc';
    TRIM : 'trim';
    RXREPLACE : 'rxreplace';
    RXMATCH : 'rxmatch';
    UPPER : 'upper';
    LOWER : 'lower';


// Tags
    // Strings
        ANIMETITLEPREFERRED : 'animetitlepreferred' | 'animetitle';
        ANIMETITLEROMAJI : 'animetitleromaji';
        ANIMETITLEENGLISH : 'animetitleenglish';
        ANIMETITLEJAPANESE : 'animetitlejapanese';
        EPISODETITLEROMAJI : 'episodetitleromaji';
        EPISODETITLEENGLISH : 'episodetitleenglish';
        EPISODETITLEJAPANESE : 'episodetitlejapanese';
        GROUPSHORT : 'groupshort';
        GROUPLONG : 'grouplong';
        CRCLOWER : 'crclower';
        CRCUPPER : 'crcupper';
        SOURCE : 'source';
        RESOLUTION : 'resolution';
        ANIMETYPE : 'animetype';
        EPISODETYPE : 'episodetype';
        EPISODEPREFIX : 'episodeprefix';
        VIDEOCODECLONG : 'videocodeclong';
        VIDEOCODECSHORT : 'videocodecshort';
        DURATION : 'duration';
        GROUPNAME : 'groupname';
        OLDFILENAME : 'oldfilename';
        ORIGINALFILENAME : 'originalfilename';
        ANIMERELEASEDATE : 'animereleasedate';
        EPISODERELEASEDATE : 'episodereleasedate';
        FILERELEASEDATE : 'filereleasedate';
        OLDIMPORTFOLDER : 'oldimportfolder';
        VIDEOCODECANIDB : 'videocodecanidb';
        EPISODENUMBERS : 'episodenumbers';

        // Date Fields
            YEAR : 'year';
            MONTH : 'month';
            DAY : 'day';

    // Numbers
        ANIMEID : 'animeid';
        EPISODEID : 'episodeid';
        EPISODENUMBER : 'episodenumber';
        VERSION : 'version';
        WIDTH : 'width';
        HEIGHT : 'height';
        EPISODECOUNT : 'episodecount';
        BITDEPTH : 'bitdepth';
        AUDIOCHANNELS : 'audiochannels';
        SERIESINGROUP : 'seriesingroup';
        LASTEPISODENUMBER : 'lastepisodenumber';
        MAXEPISODECOUNT : 'maxepisodecount';

    // Bools
        RESTRICTED : 'restricted';
        CENSORED : 'censored';
        CHAPTERED : 'chaptered';
        MANUALLYLINKED : 'manuallylinked';
        INDROPSOURCE : 'indropsource';
        MULTILINKED : 'multilinked';

    // Collections
        AUDIOCODECS : 'audiocodecs';
        DUBLANGUAGES : 'dublanguages';
        SUBLANGUAGES : 'sublanguages';
        ANIMETITLES : 'animetitles';
        EPISODETITLES : 'episodetitles';
        IMPORTFOLDERS : 'importfolders';

    // Enums
        // Languages
        
            LANGUAGE_ENUM
                :   'unknown'
                |   'english'
                |   'romaji'
                |   'japanese'
                |   'afrikaans'
                |   'arabic'
                |   'bangladeshi'
                |   'bulgarian'
                |   'frenchcanadian'
                |   'czech'
                |   'danish'
                |   'german'
                |   'greek'
                |   'spanish'
                |   'estonian'
                |   'finnish'
                |   'french'
                |   'galician'
                |   'hebrew'
                |   'hungarian'
                |   'italian'
                |   'korean'
                |   'lithuania'
                |   'mongolian'
                |   'malaysian'
                |   'dutch'
                |   'norwegian'
                |   'polish'
                |   'portuguese'
                |   'brazilianportuguese'
                |   'romanian'
                |   'russian'
                |   'slovak'
                |   'slovenian'
                |   'serbian'
                |   'swedish'
                |   'thai'
                |   'turkish'
                |   'ukrainian'
                |   'vietnamese'
                |   'chinese'
                |   'chinesesimplified'
                |   'chinesetraditional'
                |   'pinyin'
                |   'latin'
                |   'albanian'
                |   'basque'
                |   'bengali'
                |   'bosnian'
                ;

        // TitleType
            TITLETYPE_ENUM
                :   'main'
                |   'none'
                |   'official'
                |   'short'
                |   'synonym'
                ;

        // EpisodeType
            EPISODETYPE_ENUM
                :	'episode'
                |	'credits'
                |	'special'
                |	'trailer'
                |	'parody'
                |	OTHER
                ;


        //AnimeType
            ANIMETYPE_ENUM
                :	'movie'
                |	'ova'
                |	'tvseries'
                |	'tvspecial'
                |	'web'
                |   OTHER
                ;
        
        //Shared between Episode and anime types
        OTHER : 'other';


// Literals
    NUMBER
        :   [+-]? INTEGER
        ;

    fragment INTEGER
        :   DIGIT+
        ;

    fragment DIGIT
        :   [0-9]
        ;

    BOOLEAN
        :   ('true' | 'false')
        ;

    STRING
        :   '\'' .*? '\''
        |   '"' .*? '"'
        ;

//Whitespace
    UNICODE_WS
        :   [\p{White_Space}] -> skip
        ;

// Comments
    BlockComment
        :   '/*' .*? '*/' -> skip
        ;

    LineComment
        :   '//' ~[\r\n]* -> skip
        ;