Feature: New Meeting
    As a meeting organizer
	I want to be able to schedule a new user group meeting
	So that I can open registration for the event

Scenario: Schedule a meeting
	Given I have a speaker Ian Cooper
	And I have a venue EMC
	And I have a meeting date 10-AUG-2011
	And I have a capacity of 100
	When I schedule a meeting
	Then the new meeting should be open for registration
	And the date should be 10-AUG-2011
	And 150 tickets should be available
