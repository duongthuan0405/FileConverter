while ($true) {
    Get-Date -Format "HH:mm:ss.fff"

    docker exec gotenberg-test sh -c `
      "ps -eo pid,ppid,stat,etime,pcpu,args | grep '[s]office'"

    Start-Sleep -Milliseconds 1000
}