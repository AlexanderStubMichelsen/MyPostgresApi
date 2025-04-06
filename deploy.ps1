# Variables
$server = "root@172.105.95.18"
$targetPath = "../var/www/MyPostgresApi"

Write-Host "🛠 Publishing..."
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish

Write-Host "🚀 Uploading..."
scp -r ./publish/* "${server}:${targetPath}"

Write-Host "🔄 Restarting backend..."
ssh $server "sudo systemctl restart MyPostgresApi && sudo systemctl status MyPostgresApi --no-pager"
