## ADDED Requirements

### Requirement: Composite message dispatch
The event service SHALL raise an observable event for every payload type present in a received `JsonDataMessage`, independently. A message that contains both body text and one or more attachments MUST raise both the text-message event and the attachment event.

#### Scenario: Captioned attachment
- **WHEN** an incoming envelope has a `DataMessage` with a non-empty `Message` and a non-empty `Attachments` list
- **THEN** the `TextMessages` observable emits the text event
- **AND** the `Attachments` observable emits the attachment event

#### Scenario: Reaction with no body
- **WHEN** an incoming envelope has a `DataMessage` with a `Reaction` and an empty `Message`
- **THEN** the `Reaction` observable emits exactly one event
- **AND** the `TextMessages` observable emits nothing

### Requirement: Subscription-scoped routing
The event service SHALL only emit events for subscriptions it currently tracks, and SHALL resolve the owning account for each emitted event.

#### Scenario: Unknown subscription id
- **WHEN** a notification arrives for a subscription id not present in the active subscription map
- **THEN** no observable emits and the notification is ignored without throwing
