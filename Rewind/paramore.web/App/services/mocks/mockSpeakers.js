define([], function () {
    //MOCK: Get the real data frome a REST endpoint - use fiddler to capture
    /*
    [{
        "Bio":"Oscar the Grouch is a Muppet character on the television program Sesame Street. He has a green body (during the first season he was orange), has no visible nose, and lives in a trash can. His favorite thing in life is trash, as evidenced by the song 'I Love Trash'.",
        "EmailAddress":"grouch@sesamestreet.com",
        "Id":"c5ab0611-6def-44e3-ba8a-cb20bcce6ac4",
        "Links":[{"HRef":"http:\/\/localhost:59280\/speaker\/c5ab0611-6def-44e3-ba8a-cb20bcce6ac4","Rel":"self"}],
        "Name":"Oscar Grouch",
        "PhoneNumber":"666-666-6666",
        "Version":1}]
    */
    var data = [
        {
            "Bio": "Oscar the Grouch is a Muppet character on the television program Sesame Street. He has a green body (during the first season he was orange), has no visible nose, and lives in a trash can. His favorite thing in life is trash, as evidenced by the song 'I Love Trash'.",
            "EmailAddress": "grouch@sesamestreet.com",
            "Id": "c5ab0611-6def-44e3-ba8a-cb20bcce6ac4",
            "Links": [{ "HRef": "http:\/\/localhost:59280\/speaker\/c5ab0611-6def-44e3-ba8a-cb20bcce6ac4", "Rel": "self" }],
            "Name": "Oscar Grouch",
            "PhoneNumber": "666-666-6666",
            "Version": 1
        }];

    var mockSpeakers = {
      data: data  
    };

    return mockSpeakers;
});