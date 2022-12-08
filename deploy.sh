# 変数を定義する
region='japaneast'                       # デプロイ先のリージョン
resourceGroupName=$1                     # デプロイ先のリソースグループ (スクリプトの引数から取得する)
blobContainerName='uploaddata'           # オーディオファイルアップロードに使用する Blob コンテナ名
cognitiveSearchIndexName='audio-phrases' # Cognitive Search のインデックス名
functionName='ProcessUploadedData'       # Azure Functions アプリケーションコードで指定したものと同じにする

# リソースグループを作成する
az group create \
    --location $region \
    --resource-group $resourceGroupName

# 必要な Azure リソース(Storage, Speech Services, Cognitive Search, Function App など)をデプロイする
outputs=($(az deployment group create \
            --resource-group $resourceGroupName \
            --template-file infra/deploy.bicep \
            --parameters blobContainerName=$blobContainerName \
                         cognitiveSearchIndexName=$cognitiveSearchIndexName \
            --query 'properties.outputs.*.value' \
            --output tsv))
storageAccountName=`echo ${outputs[0]}` # 文末の \r を削除する
cognitiveSearchName=`echo ${outputs[1]}` # 文末の \r を削除する
functionAppName=${outputs[2]}

# Azure Functions のアプリケーションをデプロイする
pushd app
sleep 10 # Azure Functions App リソースの作成からコードデプロイが早すぎると「リソースが見つからない」エラーが発生する場合があるので、一時停止する
func azure functionapp publish $functionAppName --csharp
popd

# EventGrid をデプロイする
az deployment group create \
    --resource-group $resourceGroupName \
    --template-file infra/post-deploy.bicep \
    --parameters storageAccountName=$storageAccountName \
                 blobContainerName=$blobContainerName \
                 functionAppName=$functionAppName \
                 functionName=$functionName

# Cognitive Search のインデックスを作成する
cognitiveSearchApiKey=`az search admin-key show --service-name $cognitiveSearchName --resource-group $resourceGroupName --query 'primaryKey' --output tsv`
curl -X PUT https://$cognitiveSearchName.search.windows.net/indexes/$cognitiveSearchIndexName?api-version=2020-06-30 \
    -H 'Content-Type: application/json' \
    -H 'api-key: '$cognitiveSearchApiKey \
    -d @infra/index.json

# Web アプリで使用する情報を JSON ファイルとして出力する (Cognitive Searviceの名前とクエリキー)
queryKey=`az search query-key list --resource-group $resourceGroupName --service-name $cognitiveSearchName --query "[0].key" --output tsv`
echo "const settings = { 
    \"functionAppName\": \"$functionAppName\", 
    \"cognitiveSearchName\": \"$cognitiveSearchName\", 
    \"indexName\": \"$cognitiveSearchIndexName\", 
    \"queryKey\": \"$queryKey\" 
};" > html/settings.js

echo ''
echo '"html/upload-audio.html"を Web ブラウザで開いて WAV オーディオファイルをアップロードしてください。'
echo 'Speech Services での文字起こしが完了すると、"html/upload-audio.html" で指定した文言を話しているオーディオファイルを検索することができます。'