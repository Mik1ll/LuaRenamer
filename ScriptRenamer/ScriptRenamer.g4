grammar ScriptRenamer;
// Rules
    start : stmt* EOF;
    // Statements
        stmt
            :   if_stmt
            |   target_labels? op=ADD string_atom+
            |   target_labels? op=SET string_atom+
            |   target_labels? op=REPLACE string_atom string_atom
            |   cancel=CANCEL string_atom*
            |   cancel=(SKIPRENAME | SKIPMOVE)
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
            :   AUDIOCODECS HAS string_atom
            |   langs=(DUBLANGUAGES | SUBLANGUAGES) HAS LANGUAGE_ENUM
            |   IMPORTFOLDERS HAS string_atom
            |   titles=(ANIMETITLES | EPISODETITLES) HAS (l=LANGUAGE_ENUM (AND t=TITLETYPE_ENUM)? | t=TITLETYPE_ENUM (AND l=LANGUAGE_ENUM)?)
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
            |   op=REPLACE LPAREN string_atom COMMA string_atom COMMA string_atom RPAREN
            |   op=SUBSTRING LPAREN string_atom COMMA number_atom (COMMA number_atom)? RPAREN
            |   op=TRUNCATE LPAREN string_atom COMMA number_atom RPAREN
            |   op=TRIM LPAREN string_atom RPAREN
            ;

        date_atom
            :   type=(ANIMERELEASEDATE | EPISDOERELEASEDATE | FILERELEASEDATE) (DOT field=(DAY | MONTH | YEAR))?
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
            |   INDROPSOURCE)
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
            |   EPISODENUMBERS)
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
    SKIPRENAME : 'skipRename';
    SKIPMOVE : 'skipMove';

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
    SUBSTRING : 'substr';
    TRUNCATE : 'trunc';
    TRIM : 'trim';


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
        ANIMERELEASEDATE : 'AnimeReleaseDate';
        EPISDOERELEASEDATE : 'EpisodeReleaseDate';
        FILERELEASEDATE : 'FileReleaseDate';
        OLDIMPORTFOLDER : 'OldImportFolder';
        VIDEOCODECANIDB : 'VideoCodecAniDB';
        EPISODENUMBERS : 'EpisodeNumbers';

        // Date Fields
            YEAR : 'Year';
            MONTH : 'Month';
            DAY : 'Day';

    // Numbers
        ANIMEID : 'AnimeID';
        EPISODEID : 'EpisodeID';
        EPISODENUMBER : 'EpisodeNumber';
        VERSION : 'Version';
        WIDTH : 'Width';
        HEIGHT : 'Height';
        EPISODECOUNT : 'EpisodeCount';
        BITDEPTH : 'BitDepth';
        AUDIOCHANNELS : 'AudioChannels';
        SERIESINGROUP : 'SeriesInGroup';
        LASTEPISODENUMBER : 'LastEpisodeNumber';
        MAXEPISODECOUNT : 'MaxEpisodeCount';

    // Bools
        RESTRICTED : 'Restricted';
        CENSORED : 'Censored';
        CHAPTERED : 'Chaptered';
        MANUALLYLINKED : 'ManuallyLinked';
        INDROPSOURCE : 'InDropSource';

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