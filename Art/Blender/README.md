# COAST RACER Blender assets

Blender 5.2.2 LTS で制作した、10車体・3コース・30報酬・1コインの計44モデルです。
編集用ファイルは `Models/*.blend`、Unity用メッシュは `Assets/Resources/Blender/*.bytes` です。
外部のモデル素材や有料アセットは使用していません。

## 再生成

リポジトリ直下から実行します。

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python Art/Blender/build_assets.py
```

車体だけなら末尾に `-- --vehicles-only`、コースだけなら `-- --courses-only`、報酬だけなら `-- --items-only` を追加します。
道路の座標は共有の走行エンジンから出力した `tracks.json` と一致させています。
`dotnet run --project Tests/RevisionEight/RevisionEight.csproj -c Release` で座標と走行検証を更新できます。

## Blenderで編集したモデルの書き出し

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python Art/Blender/build_assets.py -- --export-existing Art/Blender/Models/vehicle_apex.blend
```

この方法では編集済みシーンを開いて書き出します。通常の全再生成はスクリプトの形状に戻るため、手作業の改修後は `--export-existing` を使ってください。
車輪・フォークの `group` と `pivot` カスタムプロパティを保持すると、Unityで操舵と車輪回転が連動します。
Blenderの座標はX右、Y奥、Z上です。書き出し時にUnityのX右、Y上、Z前へ変換し、単位はメートルです。

## 実行時形式と検査

`CRB7` はこのゲーム用のバイナリメッシュ形式です。形状・面法線・材質・可動部の支点を格納し、`RevisionSevenPresentation.cs` が読み込みます。
スマホで巨大なJSONを解析する負荷を避け、メッシュとマテリアルを共有します。影付きのリアルタイムライトを8個作らず、夜間はシェーダーの前方照射を使用します。
`manifest.json` にモデルごとのポリゴン数、容量、ハッシュを記録しています。

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python Art/Blender/validate_terrain.py
```

3コースの道路中央と両端の計5,760地点について、地形が路面を覆っていないかを検査します。結果は `Logs/revision7-terrain.json` です。画像での確認と併用してください。

新規インストールはありません。ユーザーが導入したBlenderと、既存のUnity・.NET・Python・Unity付属Node.jsを使用しました。

## 改修8

湘南はv2（約3.70km）。南向きの東側橋、島一周、北向きの西側橋、西の海岸、市街地の順路です。
`docs/REVISION_8_SHONAN_ROUTE.svg` が経路図です。`train` グループのpivotを保持すると、江ノ電モチーフの列車が共通レース時刻で移動します。
山岳のトンネル、橋の中央分離帯、シーキャンドル、烏帽子岩、線路下の盛土を追加しました。
四輪車はBlenderのBooleanで実際のホイールアーチを開け、車軸とスポークをタイヤ幅の内側へ収めています。
書き出し後は `python Art/Blender/update_manifest.py` で全44アセットの構造・ハッシュを確認し、manifestを更新してください。
