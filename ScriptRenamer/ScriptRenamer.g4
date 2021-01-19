// Define a grammar called Hello
grammar ScriptRenamer;
start : stmt* EOF;

stmt
    :   if_stmt
    |   non_if_stmt
    ;

if_stmt
    :   IF LPAREN bool_expr RPAREN (non_if_stmt ELSE false_branch=stmt | true_branch=stmt)
    ;

non_if_stmt
    :   add_stmt
    |   replace_stmt
    |   set_stmt
    |   block
    ;

set_stmt
    : target SET string_atom+
    ;

bool_expr
    :   op=NOT bool_expr
    |   collection_expr
    |   ANIMETYPE op=IS animeType_enum
    |   number_atom op=(GT | GE | LT | LE) number_atom
    |   number_atom op=(EQ | NE) number_atom
    |   string_atom op=(EQ | NE) string_atom
    |   bool_expr op=AND bool_expr
    |   bool_expr op=OR bool_expr
    |   LPAREN bool_expr RPAREN
    |   bool_atom
    ;

collection_expr
    :   AUDIOCODECS HAS codec_enum
    |   langs=(DUBLANGUAGES | SUBLANGUAGES) HAS language_enum
    |   IMPORTFOLDERS HAS STRING
    |   title_collection_expr
    ;

title_collection_expr
    :   title_collection_expr HAS language_enum
    |   title_collection_expr HAS titleType_enum
    |   titles=(ANIMETITLES | EPISODETITLES) HAS (language_enum | titleType_enum)
    ;

animeType_enum
    :   MOVIE
    |   OVA
    |   TVSERIES
    |   TVSPECIAL
    |   WEB
    |   OTHER
    ;

titleType_enum
    :   MAIN
    |   NONE
    |   OFFICIAL
    |   SHORT
    |   SYNONYM
    ;

codec_enum
    : STRING
    ;

language_enum
    :   lang=(UNKNOWN
    |   ENGLISH
    |   ROMAJI
    |   JAPANESE
    |   AFRIKAANS
    |   ARABIC
    |   BANGLADESHI
    |   BULGARIAN
    |   FRENCHCANADIAN
    |   CZECH
    |   DANISH
    |   GERMAN
    |   GREEK
    |   SPANISH
    |   ESTONIAN
    |   FINNISH
    |   FRENCH
    |   GALICIAN
    |   HEBREW
    |   HUNGARIAN
    |   ITALIAN
    |   KOREAN
    |   LITHUANIA
    |   MONGOLIAN
    |   MALAYSIAN
    |   DUTCH
    |   NORWEGIAN
    |   POLISH
    |   PORTUGUESE
    |   BRAZILIANPORTUGUESE
    |   ROMANIAN
    |   RUSSIAN
    |   SLOVAK
    |   SLOVENIAN
    |   SERBIAN
    |   SWEDISH
    |   THAI
    |   TURKISH
    |   UKRAINIAN
    |   VIETNAMESE
    |   CHINESE
    |   CHINESESIMPLIFIED
    |   CHINESETRADITIONAL
    |   PINYIN
    |   LATIN
    |   ALBANIAN
    |   BASQUE
    |   BENGALI
    |   BOSNIAN)
    ;

bool_atom
    :   BOOLEAN
    |   bool_labels
    ;

bool_labels
    :   string_labels
    |   RESTRICTED
    |   CENSORED
    |   CHAPTERED
    ;

number_atom
    :   NUMBER
    |   number_labels
    ;

number_labels
    :   EPISODENUMBER
    |   FILEVERSION
    |   WIDTH
    |   HEIGHT
    |   YEAR
    |   EPISODECOUNT
    |   BITDEPTH
    |   AUDIOCHANNELS
    ;

add_stmt
    :   target ADD string_atom+
    ;

string_atom
    :   STRING
    |   string_labels
    |   number_labels
    |   collection_labels
    ;

collection_labels
    :   AUDIOCODECS
    |   DUBLANGUAGES
    |   SUBLANGUAGES
    |   ANIMETITLES
    |   EPISODETITLES
    |   IMPORTFOLDERS
    ;

string_labels
    :   ANIMETITLEPREFERRED
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
    |   VIDEOCODECLONG
    |   VIDEOCODECSHORT
    |   DURATION
    |   GROUPNAME
    |   OLDFILENAME
    |   ORIGINALFILENAME
    ;

replace_stmt
    : target REPLACE STRING STRING
    ;

block
    :   LBRACK stmt* RBRACK
    ;

target
    :   tar=(FILENAME | DESTINATION | SUBFOLDER)
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
        VIDEOCODECLONG : 'VideoCodecLong';
        VIDEOCODECSHORT : 'VideoCodecShort';
        DURATION : 'Duration';
        GROUPNAME : 'GroupName';
        OLDFILENAME : 'OldFilename';
        ORIGINALFILENAME : 'OriginalFilename';

    // Numbers
        EPISODENUMBER : 'EpisodeNumber';
        FILEVERSION : 'FileVersion';
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
            UNKNOWN : 'Unknown';
            ENGLISH : 'English';
            ROMAJI : 'Romaji';
            JAPANESE : 'Japanese';
            AFRIKAANS : 'Afrikaans';
            ARABIC : 'Arabic';
            BANGLADESHI : 'Bangladeshi';
            BULGARIAN : 'Bulgarian';
            FRENCHCANADIAN : 'FrenchCanadian';
            CZECH : 'Czech';
            DANISH : 'Danish';
            GERMAN : 'German';
            GREEK : 'Greek';
            SPANISH : 'Spanish';
            ESTONIAN : 'Estonian';
            FINNISH : 'Finnish';
            FRENCH : 'French';
            GALICIAN : 'Galician';
            HEBREW : 'Hebrew';
            HUNGARIAN : 'Hungarian';
            ITALIAN : 'Italian';
            KOREAN : 'Korean';
            LITHUANIA : 'Lithuania';
            MONGOLIAN : 'Mongolian';
            MALAYSIAN : 'Malaysian';
            DUTCH : 'Dutch';
            NORWEGIAN : 'Norwegian';
            POLISH : 'Polish';
            PORTUGUESE : 'Portuguese';
            BRAZILIANPORTUGUESE : 'BrazilianPortuguese';
            ROMANIAN : 'Romanian';
            RUSSIAN : 'Russian';
            SLOVAK : 'Slovak';
            SLOVENIAN : 'Slovenian';
            SERBIAN : 'Serbian';
            SWEDISH : 'Swedish';
            THAI : 'Thai';
            TURKISH : 'Turkish';
            UKRAINIAN : 'Ukrainian';
            VIETNAMESE : 'Vietnamese';
            CHINESE : 'Chinese';
            CHINESESIMPLIFIED : 'ChineseSimplified';
            CHINESETRADITIONAL : 'ChineseTraditional';
            PINYIN : 'Pinyin';
            LATIN : 'Latin';
            ALBANIAN : 'Albanian';
            BASQUE : 'Basque';
            BENGALI : 'Bengali';
            BOSNIAN : 'Bosnian';

        // TitleType
            MAIN : 'Main';
            NONE : 'None';
            OFFICIAL : 'Official';
            SHORT : 'Short';
            SYNONYM : 'Synonym';

        // EpisodeType
            EPISODE : 'Episode';
            CREDITS : 'Credits';
            SPECIAL : 'Special';
            TRAILER : 'Trailer';
            PARODY : 'Parody';
            OTHER : 'Other';

        //AnimeType
            MOVIE : 'Movie';
            OVA : 'OVA';
            TVSERIES : 'TVSeries';
            TVSPECIAL : 'TVSpecial';
            WEB : 'Web';
            // OTHER : 'Other'; (Shared with EpisodeType)


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

//Whitespace
    WS : [ \t\r\n]+ -> skip ; // skip spaces, tabs, newlines
    UNICODE_WS : [\p{White_Space}] -> skip; // match all Unicode whitespace

// Comments
    BlockComment
        :   '/*' .*? '*/'
            -> skip
        ;

    LineComment
        :   '//' ~[\r\n]*
            -> skip
        ;