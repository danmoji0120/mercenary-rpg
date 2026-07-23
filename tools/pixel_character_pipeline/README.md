# 캐릭터 픽셀 변환 배치 도구

이 도구는 투명 전신 캐릭터 PNG를 게임용 픽셀풍 PNG로 기계적으로 변환합니다. 캐릭터를 다시 그리거나 재설계하지 않으며, 원본에 있는 무기·장식·이펙트를 제거하지 않습니다. 원본의 전신 비율과 전체 실루엣을 유지하므로 실제 게임 표시 크기에서 프리셋 결과를 비교해야 합니다.

핵심 축소·최근접 확대와 K-means 감색 방식은 [tsutsuji815/pixel_convert](https://github.com/tsutsuji815/pixel_convert)를 바탕으로 수정했습니다. 원본은 BSD 2-Clause 라이선스이며 전문은 `THIRD_PARTY_LICENSES/pixel_convert_LICENSE.txt`에 보존되어 있습니다. Flask 웹 서버와 웹 UI는 포함하지 않습니다.

## 설치

Python 3.11 이상을 권장합니다. 프로젝트 루트에서 다음처럼 별도 가상환경을 만들 수 있습니다.

```powershell
py -3.11 -m venv .venv-pixel
.\.venv-pixel\Scripts\Activate.ps1
python -m pip install -r tools\pixel_character_pipeline\requirements.txt
```

필수 패키지는 Pillow, NumPy, opencv-python-headless뿐입니다.

## 기본 경로

- 입력: `asset_work\character_sources`
- 출력: `asset_work\character_pixelized\<preset>`
- JSON/CSV 보고서: `asset_work\character_pixel_reports`
- Contact Sheet: `asset_work\character_contact_sheets`

입력 폴더는 재귀 탐색하며 하위 폴더 구조와 유니코드 파일명을 출력에도 보존합니다. 입력 PNG는 읽기 전용으로 다루며 덮어쓰거나 삭제하지 않습니다.

## 프리셋

모든 값은 `presets.json`에서 관리합니다.

| 프리셋 | 콘텐츠 높이 | 픽셀 블록 | 요청 팔레트 | 채도 배율 |
|---|---:|---:|---:|---:|
| `world_64` | 64 | 2 | 16 | 1.35 |
| `world_80` | 80 | 2 | 20 | 1.35 |
| `world_96` | 96 | 3 | 24 | 1.35 |
| `world_128_detail` | 128 | 2 | 32 | 1.35 |

출력 폭은 종횡비에 따라 동적으로 정해집니다. 콘텐츠의 최하단 불투명 픽셀을 하단 기준으로 정렬하고, 캔버스 크기는 픽셀 블록의 배수로 올림합니다.

## 실행

첫 8개를 네 프리셋으로 미리 보기:

```bat
tools\pixel_character_pipeline\run_pixel_preview.bat
```

기본 `world_96` 전체 배치:

```bat
tools\pixel_character_pipeline\run_pixel_batch.bat
```

직접 실행 예:

```powershell
python tools\pixel_character_pipeline\batch_convert.py --preset world_96 --limit 8 --overwrite
python tools\pixel_character_pipeline\batch_convert.py --all-presets
python tools\pixel_character_pipeline\batch_convert.py --preset world_96 --include "g_human_*"
```

주요 옵션:

- `--input-dir`, `--output-dir`: 입력과 출력 루트
- `--preset`, `--all-presets`: 하나 또는 모든 프리셋
- `--limit`, `--include`, `--exclude`: 결정적으로 정렬된 입력 선택
- `--overwrite`: 기존 결과가 있어도 이번 실행에서 다시 생성
- `--force-rebuild`: resume 메타데이터를 무시하고 재생성
- `--dry-run`: 파일 목록과 보고서만 확인
- `--workers`: 동시 변환 프로세스 수(기본 1)
- `--saturation none|low|medium|high`: 프리셋 채도 단계 덮어쓰기
- `--saturation-multiplier`: 채도 배율 직접 덮어쓰기
- `--no-contact-sheet`: Contact Sheet 생략
- `--verbose`: 파일별 상태 출력

## Resume와 보고서

각 PNG 옆의 `.png.json` sidecar에는 source SHA-256, 프리셋 설정 hash, 알고리즘 버전, output SHA-256이 기록됩니다. 네 값이 일치하면 기본 실행은 `SKIPPED`로 처리합니다. 원본이나 프리셋이 바뀌면 해당 결과만 자동 재생성됩니다.

실행마다 타임스탬프 JSON과 CSV를 만들고 `pixel_conversion_latest.json`을 갱신합니다. 개별 파일 실패는 `FAILED`와 오류 내용을 기록한 뒤 다음 파일을 계속 처리합니다.

Contact Sheet는 프리셋별 최대 24개씩 페이지를 나누고 checkerboard 배경 위에 최근접 확대 미리 보기를 표시합니다. 첫 8개를 `--all-presets`로 처리하면 Source와 네 프리셋을 나란히 놓은 `pixel_preset_comparison_test.png`도 생성합니다.

## 처리 원칙

- 알파 임계값보다 큰 픽셀만 콘텐츠 Bounding Box와 K-means 입력에 사용합니다.
- 색상 감색 전에 불투명 영역의 채도만 높이며 Hue와 Value는 바꾸지 않습니다.
- 최종 투명 픽셀의 RGB는 0으로 정리하고 알파는 0 또는 255로 양자화합니다.
- source hash, preset, 알고리즘 버전에서 만든 안정적인 seed를 사용해 결과를 결정적으로 만듭니다.
- 모든 원본 고해상도 이미지를 한꺼번에 메모리에 적재하지 않습니다.

## Self-check

```powershell
python tools\pixel_character_pipeline\self_check.py
```

Synthetic fixture로 투명도, 숨은 RGB 오염, 바이트 결정성, 크기, 팔레트 축소, 빈 이미지 격리, 유니코드 경로, 원본 보호, resume, worker 수 결정성, 채도 불변식을 검사합니다.

## 자주 발생하는 문제

- `EMPTY_ALPHA`: 임계값보다 큰 알파 픽셀이 없습니다. `alpha_threshold`와 원본을 확인합니다.
- 결과가 너무 거칠거나 세밀함: 콘텐츠 높이와 `pixel_block_size`가 다른 프리셋을 Contact Sheet에서 비교합니다.
- 팔레트가 요청보다 작음: 원본의 실제 고유색 또는 불투명 논리 픽셀이 적어 정상적으로 축소된 것입니다.
- 검은 배경처럼 보임: PNG 자체는 투명할 수 있으므로 알파를 지원하는 뷰어에서 확인합니다.
- OpenCV import 실패: 활성 Python 환경에서 requirements를 다시 설치합니다.

## Godot에서 사용 시

승인한 출력만 수동으로 게임 에셋 폴더에 복사하십시오. 이 도구는 자동 배포하거나 Godot Resource를 수정하지 않습니다. Godot Import 설정에서 Texture Filter를 `Nearest`로 사용하면 픽셀 블록이 흐려지지 않습니다.
