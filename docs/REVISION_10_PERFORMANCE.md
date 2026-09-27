# 改修10 描画負荷の確認

2026-09-27。改修9 Build 44db0ee151c0 と改修10 Build 8d24358e4132。

このPCでは軽量設定の測定場面で約30回／秒の描画更新を確認した。改修10の24サンプルは29.68～30.13回／秒、描画命令が出たフレームの間隔のp95は最大33.8ms。実行時エラー0。

## 測定条件と限界
- Headless Edge 154、AMD Radeon(TM) Graphics / Direct3D11。SwiftShaderではない。
- 1100×650、軽量設定0（30fps上限）、8台をロード、HUDを非表示、静止場面。自車以外は主に後方におり、全車・全エフェクトが同時に映る最悪条件ではない。
- 山道・海岸・洞窟・道の駅・ETC・高速道路を昼夜で確認。各条件3.5秒待機後、4.5秒×2回。改修9の比較8サンプルを含め合計32。
- WebGL描画命令の発行を数えた値。requestAnimationFrameだけをゲームFPSと数えていない。GPUの処理完了時間やディスプレイの実表示FPSではない。
- コース形状が変わるため代表的な場面同士の比較。旧版と完全に同一のカメラ画像ではない。
- スマホ実機、走行シミュレーションとの同時負荷、長時間発熱、通信、音響、スペシャル集中時はこの測定の対象外。

## 結果
描画更新は2回平均、p95は2回の悪い方。三角形は描画命令に投入された数であり、最終的に画面に見える数ではない。

| Version | Scene | Night | Draw updates/s | p95 ms | Draw calls | Triangles |
|---|---|---|---:|---:|---:|---:|
| revision9 | hill | False | 29.91 | 33.4 | 32 | 81,193 |
| revision9 | hill | True | 29.91 | 33.4 | 32 | 81,193 |
| revision9 | coast | False | 29.90 | 33.7 | 33 | 80,570 |
| revision9 | coast | True | 30.13 | 33.6 | 33 | 80,570 |
| revision10 | hill | False | 29.91 | 33.7 | 34 | 114,407 |
| revision10 | hill | True | 30.02 | 33.8 | 34 | 114,407 |
| revision10 | coast | False | 30.02 | 33.7 | 35 | 104,147 |
| revision10 | coast | True | 29.90 | 33.7 | 35 | 104,147 |
| revision10 | cave | False | 30.02 | 33.8 | 40 | 115,795 |
| revision10 | cave | True | 30.02 | 33.6 | 40 | 115,795 |
| revision10 | station | False | 30.02 | 33.8 | 32 | 103,355 |
| revision10 | station | True | 29.91 | 33.7 | 32 | 103,355 |
| revision10 | etc | False | 30.02 | 33.7 | 36 | 106,597 |
| revision10 | etc | True | 29.90 | 33.7 | 36 | 106,597 |
| revision10 | highway | False | 30.02 | 33.8 | 31 | 103,115 |
| revision10 | highway | True | 30.02 | 33.6 | 31 | 103,115 |

## 規模の変化
湘南コースモデルは79,426から112,972三角形へ約42.2％増加。資産全体の増加と、カメラから見た描画負荷は同じではない。今回のPCの測定では軽量設定の上限を維持したが、スマホでの余裕を保証するものではない。洞窟の固定灯は多数のリアルタイムライトを追加せず発光材質で表現し、既存の軽量シェーダーを利用した。

生データ: [REVISION_10_PERFORMANCE.json](REVISION_10_PERFORMANCE.json)。
測定スクリプト: Deployment/Test-Revision-Ten-Performance.mjs。
