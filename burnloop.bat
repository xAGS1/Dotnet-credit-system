@echo off
setlocal EnableDelayedExpansion

set TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkNTU1ZjkzMi03NjI3LTRjNzMtODJlOC1jM2JkNWFiMmZmYTQiLCJlbWFpbCI6ImhpY2hhbUBleGFtcGxlLmNvbSIsInVzZXJuYW1lIjoiaGljaGFtIiwiZXhwIjoxNzcwMzYxNTY2LCJpc3MiOiJDcmVkaXRUYXNrc0FwaSIsImF1ZCI6IkNyZWRpdFRhc2tzQXBpIn0.TtIWUVYps_EZQZlQufmqyhxmdOpohrud5g28WcSFdlg

for /L %%i in (1,1,200) do (
  echo ==== Iteration %%i ====

  rem Create task and extract id (naive parse, works with current JSON format)
  for /f "tokens=2 delims=:," %%a in ('
    curl -s -k -X POST "https://localhost:7213/tasks" ^
      -H "accept: application/json" ^
      -H "Content-Type: application/json" ^
      -H "Authorization: Bearer %TOKEN%" ^
      -d "{\"name\":\"burn\"}"
  ') do (
    set id=%%a
    goto :gotid
  )

  :gotid
  set id=!id:"=!
  echo Created !id!

  rem Execute task
  curl -s -k -X POST "https://localhost:7213/tasks/!id!/execute" ^
    -H "accept: application/json" ^
    -H "Authorization: Bearer %TOKEN%"
  echo.

  rem Show credits
  curl -s -k -X GET "https://localhost:7213/me" ^
    -H "accept: application/json" ^
    -H "Authorization: Bearer %TOKEN%"
  echo.
  echo.

)

endlocal
