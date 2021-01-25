// Define a grammar called Hello
grammar ScriptRenamer;
// Rules
    start : stmt* EOF;
    // Statements
        stmt
            :   if_stmt
            |   target_labels? op=ADD string_atom+
            |   target_labels? op=SET string_atom+
            |   target_labels? op=REPLACE string_atom string_atom
            |   block
            ;

        if_stmt
            :   IF LPAREN bool_expr RPAREN (true_branch=stmt | true_branch=stmt ELSE false_branch=stmt)
            ;

        block
            :   LBRACK stmt* RBRACK
            ;

    // Expressions
        bool_expr
            :   op=NOT bool_expr
            |   collection_expr
            |   is_left=ANIMETYPE op=IS ANIMETYPE_ENUM
            |   is_left=EPISODETYPE op=IS EPISODETYPE_ENUM
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
            :   AUDIOCODECS HAS string_atom
            |   langs=(DUBLANGUAGES | SUBLANGUAGES) HAS LANGUAGE_ENUM
            |   IMPORTFOLDERS HAS string_atom
            |   FIRST LPAREN collection_expr RPAREN
            |   title_collection_expr
            |   collection_labels
            ;

        title_collection_expr
            :   title_collection_expr HAS rhs=(LANGUAGE_ENUM | TITLETYPE_ENUM)
            |   lhs=(ANIMETITLES | EPISODETITLES) HAS rhs=(LANGUAGE_ENUM | TITLETYPE_ENUM)
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
            :   STRING
            |   string_labels
            |   collection_expr
            |   number_atom
            ;

    // Labels
        number_labels
            :   label=(EPISODENUMBER
            |   VERSION
            |   WIDTH
            |   HEIGHT
            |   YEAR
            |   EPISODECOUNT
            |   BITDEPTH
            |   AUDIOCHANNELS)
            ;

        bool_labels
            :   label=(RESTRICTED
            |   CENSORED
            |   CHAPTERED)
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
            |   ORIGINALFILENAME)
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

// Tags
    // Strings
        ANIMETITLEPREFERRED : 'AnimeTitlePreferred' | 'AnimeTitle';
        ANIMETITLEROMAJI : 'AnimeTitleRomaji';
        ANIMETITLEENGLISH : 'AnimeTitleEnglish';
        ANIMETITLEJAPANESE : 'AnimeTitleJapanese';
        EPISODETITLEROMAJI : 'EpisodeTitleRomaji';
        EPISODETITLEENGLISH : 'EpisodeTitleEnglish';
        EPISODETITLEJAPANESE : 'EpisodeTitleJapanese';
        GROUPSHORT : 'GroupShort';
        GROUPLONG : 'GroupLong';
        CRCLOWER : 'CRCLower';
        CRCUPPER : 'CRCUpper';
        SOURCE : 'Source';
        RESOLUTION : 'Resolution';
        ANIMETYPE : 'AnimeType';
        EPISODETYPE : 'EpisodeType';
        EPISODEPREFIX : 'EpisodePrefix';
        VIDEOCODECLONG : 'VideoCodecLong';
        VIDEOCODECSHORT : 'VideoCodecShort';
        DURATION : 'Duration';
        GROUPNAME : 'GroupName';
        OLDFILENAME : 'OldFilename';
        ORIGINALFILENAME : 'OriginalFilename';

    // Numbers
        EPISODENUMBER : 'EpisodeNumber';
        VERSION : 'Version';
        WIDTH : 'Width';
        HEIGHT : 'Height';
        YEAR : 'Year';
        EPISODECOUNT : 'EpisodeCount';
        BITDEPTH : 'BitDepth';
        AUDIOCHANNELS : 'AudioChannels';

    // Bools
        RESTRICTED : 'Restricted';
        CENSORED : 'Censored';
        CHAPTERED : 'Chaptered';

    // Collections
        AUDIOCODECS : 'AudioCodecs';
        DUBLANGUAGES : 'DubLanguages';
        SUBLANGUAGES : 'SubLanguages';
        ANIMETITLES : 'AnimeTitles';
        EPISODETITLES : 'EpisodeTitles';
        IMPORTFOLDERS : 'ImportFolders';

    // Enums
        // Languages
        
            LANGUAGE_ENUM
                :   'Unknown'
                |   'English'
                |   'Romaji'
                |   'Japanese'
                |   'Afrikaans'
                |   'Arabic'
                |   'Bangladeshi'
                |   'Bulgarian'
                |   'FrenchCanadian'
                |   'Czech'
                |   'Danish'
                |   'German'
                |   'Greek'
                |   'Spanish'
                |   'Estonian'
                |   'Finnish'
                |   'French'
                |   'Galician'
                |   'Hebrew'
                |   'Hungarian'
                |   'Italian'
                |   'Korean'
                |   'Lithuania'
                |   'Mongolian'
                |   'Malaysian'
                |   'Dutch'
                |   'Norwegian'
                |   'Polish'
                |   'Portuguese'
                |   'BrazilianPortuguese'
                |   'Romanian'
                |   'Russian'
                |   'Slovak'
                |   'Slovenian'
                |   'Serbian'
                |   'Swedish'
                |   'Thai'
                |   'Turkish'
                |   'Ukrainian'
                |   'Vietnamese'
                |   'Chinese'
                |   'ChineseSimplified'
                |   'ChineseTraditional'
                |   'Pinyin'
                |   'Latin'
                |   'Albanian'
                |   'Basque'
                |   'Bengali'
                |   'Bosnian'
                ;

        // TitleType
            TITLETYPE_ENUM
                :   'Main'
                |   'None'
                |   'Official'
                |   'Short'
                |   'Synonym'
                ;

        // EpisodeType
            EPISODETYPE_ENUM
                :	'Episode'
                |	'Credits'
                |	'Special'
                |	'Trailer'
                |	'Parody'
                |	OTHER
                ;


        //AnimeType
            ANIMETYPE_ENUM
                :	'Movie'
                |	'OVA'
                |	'TVSeries'
                |	'TVSpecial'
                |	'Web'
                |   OTHER
                ;
        
        //Shared between Episode and anime types
        OTHER : 'Other';


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