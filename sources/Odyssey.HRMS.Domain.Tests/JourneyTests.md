

`Team1PaidHolidayEveryMonthAccrualFlowTest`
```json
[
  [
    {"Start" : "TeamImport", "Event" : "TeamEmployeeAdded", "Target" : "PaidHolidayAccrual"},
    {"Start" : "PaidHolidayAccrual", "Event" : "PaidHolidayAccrued", "Target" : "NotifyEmployee"},
    {"Start" : "NotifyEmployee", "Event" : "NotificationSent", "Target" : "EndOfJourney"}
  ],
  [
    {"Start" : "TeamImport", "Event" : "TeamEmployeeAdded", "Target" : "PaidHolidayAccrual"},
    {"Start" : "PaidHolidayAccrual", "Event" : "PaidHolidayAccrued", "Target" : "NotifyEmployee"},
    {"Start" : "NotifyEmployee", "Event" : "NotificationNotSent", "Target" : "EndOfJourney"}
  ],
  [
    {"Start" : "TeamImport", "Event" : "TeamEmployeeAdded", "Target" : "PaidHolidayAccrual"},
    {"Start" : "PaidHolidayAccrual", "Event" : "PaidHolidayAccrualFailed", "Target" : "EndOfJourney"}
  ],
  [
    {"Start" : "TeamImport", "Event" : "TeamImportFailed", "Target" : "EndOfJourney"}
  ]
]
```