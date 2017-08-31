#region Licence
/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.MessageStore.MySql
{
    public class MySqlMessageStoreBuilder
    {
        const string MessageStoreDdl = @"CREATE TABLE {0} ( 
	`MessageId` CHAR(36) NOT NULL , 
	`Topic` VARCHAR(255) NOT NULL , 
	`MessageType` VARCHAR(32) NOT NULL , 
	`Timestamp` TIMESTAMP(3) NOT NULL , 
	`HeaderBag` TEXT NOT NULL , 
	`Body` TEXT NOT NULL , 
    `Created` TIMESTAMP(3) NOT NULL DEFAULT NOW(3),
    `CreatedID` INT(11) NOT NULL AUTO_INCREMENT,
    UNIQUE(`CreatedID`),
	PRIMARY KEY (`MessageId`)
) ENGINE = InnoDB;";

        public static string GetDDL(string tableName)
        {
            return string.Format(MessageStoreDdl, tableName);
        }
    }
}
