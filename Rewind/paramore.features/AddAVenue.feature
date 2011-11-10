Feature: Addition
	In order that I have somewhere to host a user group meeting
	As an organizer
	I want to create a venue

Scenario: Create a venue
	Given That I have a venue name of Skills Matter
	When I create a new venue
	Then whe I list venues Skills Matter should be included

Scenario: Add an address to a venue
	Given I have a venue Skills Matter
	And I have a street address of 116-120 Goswell Road
	And a city of London
	And a post code of EC1V 7DP
	When I add an address to a venue and ask for directions
	Then I should get a street address of 116-120 Goswell Road
	And a city of London
	And a post code of EC1V 7DP

Scenario: Add a map to a venue
 Given I have a venue Skills Matter
 And I have a map uri of http://skillsmatter.com/go/find-us
 When I add a map to a location and ask for directions
 Then I should get a map uri of http://skillsmatter.com/go/find-us 