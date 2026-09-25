Feature: Best stories
  As a consumer of the Best Stories API
  I want the best n Hacker News stories ordered by score
  So that I can show the most relevant content without hammering Hacker News

  Background:
    Given Hacker News has the following best stories:
      | id       | title                                                        | by           | score | comments | time       | url                                                      |
      | 21233041 | A uBlock Origin update was rejected from the Chrome Web Store | ismaildonmez | 1716  | 572      | 1570887781 | https://github.com/uBlockOrigin/uBlock-issues/issues/745 |
      | 100      | Show HN: A tiny trading engine                               | alice        | 850   | 120      | 1570800000 | https://example.com/engine                               |
      | 200      | Ask HN: How do you price bonds?                              | bob          | 1200  | 310      | 1570700000 |                                                          |
      | 300      | Rust vs C# for low latency                                   | carol        | 40    | 8        | 1570600000 | https://example.com/latency                              |

  Scenario: Caller asks for the best n stories
    When I request the best 3 stories
    Then the response status should be 200
    And the stories should be returned in this order:
      | title                                                        | postedBy     | score | commentCount | uri                                                      | time                      |
      | A uBlock Origin update was rejected from the Chrome Web Store | ismaildonmez | 1716  | 572          | https://github.com/uBlockOrigin/uBlock-issues/issues/745 | 2019-10-12T13:43:01+00:00 |
      | Ask HN: How do you price bonds?                              | bob          | 1200  | 310          |                                                          | 2019-10-10T09:33:20+00:00 |
      | Show HN: A tiny trading engine                               | alice        | 850   | 120          | https://example.com/engine                               | 2019-10-11T13:20:00+00:00 |

  Scenario: Caller asks for more stories than Hacker News has
    When I request the best 50 stories
    Then the response status should be 200
    And 4 stories should be returned

  Scenario Outline: Caller asks for an invalid number of stories
    When I request the best <count> stories
    Then the response status should be 400

    Examples:
      | count |
      | 0     |
      | -1    |
      | 201   |

  Scenario: A burst of callers does not overload Hacker News
    Given Hacker News takes 300 milliseconds to respond
    When 500 clients request the best 3 stories at the same time
    Then every response status should be 200
    And Hacker News should have been asked for the best story ids 1 time
    And each story should have been fetched from Hacker News exactly once

  Scenario: A single noisy client is throttled
    Given each client may make 5 requests
    When I request the best 1 stories 6 times
    Then the last response status should be 429
