---@meta

---@enum ImportFolderType
ImportFolderType = {
    Excluded = "Excluded",
    Source = "Source",
    Destination = "Destination",
    Both = "Both"
}

---@enum AnimeType
AnimeType = {
    Movie = "Movie",
    OVA = "OVA",
    TVSeries = "TVSeries",
    TVSpecial = "TVSpecial",
    Web = "Web",
    Other = "Other"
}

---@enum EpisodeType
EpisodeType = {
    Episode = "Episode",
    Credits = "Credits",
    Special = "Special",
    Trailer = "Trailer",
    Parody = "Parody",
    Other = "Other"
}

---@enum TitleType
TitleType = {
    None = "None",
    Main = "Main",
    Official = "Official",
    Short = "Short",
    Synonym = "Synonym",
    TitleCard = "TitleCard",
    KanjiReading = "KanjiReading"
}

---@enum Language
Language = {

--#region AniDB Languages
    Japanese = "Japanese",
    Romaji = "Romaji",
    English = "English",
    Chinese = "Chinese",
    ChineseSimplified = "ChineseSimplified",
    ChineseTraditional = "ChineseTraditional",
    Pinyin = "Pinyin",
    Korean = "Korean",
    KoreanTranscription = "KoreanTranscription",
    Afrikaans = "Afrikaans",
    Albanian = "Albanian",
    Arabic = "Arabic",
    Bangladeshi = "Bangladeshi",
    Bosnian = "Bosnian",
    Bulgarian = "Bulgarian",
    MyanmarBurmese = "MyanmarBurmese",
    Croatian = "Croatian",
    Czech = "Czech",
    Danish = "Danish",
    Dutch = "Dutch",
    Esperanto = "Esperanto",
    Estonian = "Estonian",
    Filipino = "Filipino",
    Finnish = "Finnish",
    French = "French",
    Georgian = "Georgian",
    German = "German",
    Greek = "Greek",
    HaitianCreole = "HaitianCreole",
    Hebrew = "Hebrew",
    Hindi = "Hindi",
    Hungarian = "Hungarian",
    Icelandic = "Icelandic",
    Indonesian = "Indonesian",
    Italian = "Italian",
    Javanese = "Javanese",
    Latin = "Latin",
    Latvian = "Latvian",
    Lithuanian = "Lithuanian",
    Malaysian = "Malaysian",
    Mongolian = "Mongolian",
    Nepali = "Nepali",
    Norwegian = "Norwegian",
    Persian = "Persian",
    Polish = "Polish",
    Portuguese = "Portuguese",
    BrazilianPortuguese = "BrazilianPortuguese",
    Romanian = "Romanian",
    Russian = "Russian",
    Serbian = "Serbian",
    Sinhala = "Sinhala",
    Slovak = "Slovak",
    Slovenian = "Slovenian",
    Spanish = "Spanish",
    Basque = "Basque",
    Catalan = "Catalan",
    Galician = "Galician",
    Swedish = "Swedish",
    Tamil = "Tamil",
    Tatar = "Tatar",
    Telugu = "Telugu",
    Thai = "Thai",
    ThaiTranscription = "ThaiTranscription",
    Turkish = "Turkish",
    Ukrainian = "Ukrainian",
    Urdu = "Urdu",
    Vietnamese = "Vietnamese",
--#endregion

--#region Other Languages
    Amharic = "Amharic",
    Armenian = "Armenian",
    Azerbaijani = "Azerbaijani",
    Belarusian = "Belarusian",
    Bengali = "Bengali",
    Chichewa = "Chichewa",
    Corsican = "Corsican",
    Divehi = "Divehi",
    EnglishAmerican = "EnglishAmerican",
    EnglishAustralian = "EnglishAustralian",
    EnglishBritish = "EnglishBritish",
    EnglishCanadian = "EnglishCanadian",
    EnglishIndia = "EnglishIndia",
    EnglishNewZealand = "EnglishNewZealand",
    Fijian = "Fijian",
    FrenchCanadian = "FrenchCanadian",
    Gujarati = "Gujarati",
    Hausa = "Hausa",
    Igbo = "Igbo",
    Irish = "Irish",
    Kannada = "Kannada",
    Kazakh = "Kazakh",
    Khmer = "Khmer",
    Kurdish = "Kurdish",
    Kyrgyz = "Kyrgyz",
    Lao = "Lao",
    Luxembourgish = "Luxembourgish",
    Macedonian = "Macedonian",
    Malagasy = "Malagasy",
    Malayalam = "Malayalam",
    Maltese = "Maltese",
    Maori = "Maori",
    Marathi = "Marathi",
    Oriya = "Oriya",
    Pashto = "Pashto",
    Punjabi = "Punjabi",
    Quechua = "Quechua",
    Samoan = "Samoan",
    ScotsGaelic = "ScotsGaelic",
    Sesotho = "Sesotho",
    Shona = "Shona",
    Sindhi = "Sindhi",
    Somali = "Somali",
    Swahili = "Swahili",
    Tajik = "Tajik",
    Turkmen = "Turkmen",
    Uighur = "Uighur",
    Uzbek = "Uzbek",
    Welsh = "Welsh",
    Xhosa = "Xhosa",
    Yiddish = "Yiddish",
    Yoruba = "Yoruba",
    Zulu = "Zulu",
--#endregion

    Main = "Main",
    None = "None",
    Unknown = "Unknown",
}

---@enum RelationType
RelationType = {
    Other = "Other",
    SameSetting = "SameSetting",
    AlternativeSetting = "AlternativeSetting",
    AlternativeVersion = "AlternativeVersion",
    SharedCharacters = "SharedCharacters",
    Prequel = "Prequel",
    MainStory = "MainStory",
    FullStory = "FullStory",
    Sequel = "Sequel",
    SideStory = "SideStory",
    Summary = "Summary"
}
