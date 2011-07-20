Feature: New Meeting
    As a meeting organizer
	I want to be able to schedule a new user group meeting
	So that I can open registration for the event

Scenario: Schedule a meeting
	Given I have a speaker
	And I have a venue
	And I have a meeting date
	And I have a capacity
	When I schedule a meeting
	Then the new meeting should be open for registration
