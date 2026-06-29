#!/usr/bin/env python3
"""Generate search.hlds v3.0.0 - simple cmd + batch approach (no parens)."""

import json
import os
import sys

# The .bat script - MUST avoid ( at start of line (cmd would treat as block).
# Use goto-based control flow and single-line for /r.
BAT_LINES = [
    "@echo off",
    "setlocal",
    "set \"ARG=%~1\"",
    "set \"DIR=\"",
    "set \"TERM=\"",
    "if \"%ARG%\"==\"\" goto noarg",
    "if exist \"%ARG%\\\" goto isdir",
    "rem --- file path: derive parent dir and base name ---",
    "for %%F in (\"%ARG%\") do set \"DIR=%%~dpF\"",
    "for %%F in (\"%ARG%\") do set \"TERM=%%~nF\"",
    "goto resolved",
    ":isdir",
    "set \"DIR=%ARG%\"",
    "goto resolved",
    ":noarg",
    "set \"DIR=%CD%\"",
    ":resolved",
    "if not defined DIR set \"DIR=%CD%\"",
    "if defined TERM goto doterm",
    "set /p \"TERM=Enter search term (in %DIR%): \"",
    ":doterm",
    "if not defined TERM exit /b",
    "set \"OUT=%TEMP%\\lrs_search_results.txt\"",
    ">  \"%OUT%\" echo ===== LRS Search =====",
    ">> \"%OUT%\" echo Directory: %DIR%",
    ">> \"%OUT%\" echo Term:      %TERM%",
    ">> \"%OUT%\" echo ====================",
    "for /r \"%DIR%\" %%F in (*%TERM%*) do >> \"%OUT%\" echo %%~fF",
    ">> \"%OUT%\" echo ===== done =====",
    "notepad \"%OUT%\"",
]


def make_cmd(extra_args=""):
    """
    Build the full cmd.exe command:
    1. Write each line of the .bat to %TEMP%\\lrs_search.bat using echo >> file
    2. Open a NEW cmd window (visible) that runs the .bat with the given args
    """
    parts = []
    for i, line in enumerate(BAT_LINES):
        if i == 0:
            parts.append(f'> "%TEMP%\\lrs_search.bat" echo {line}')
        else:
            parts.append(f'>> "%TEMP%\\lrs_search.bat" echo {line}')

    inner = ' & '.join(parts)
    # start with title; "cmd /C" is the inner command; bat path and arg
    inner += f' & start "LRS Search" cmd /C "%TEMP%\\lrs_search.bat" {extra_args}'

    return f'cmd.exe /c {inner}'


def make_hlds():
    return {
        "id": "lrs.builtin.search",
        "name": "搜索文件 / File Search",
        "version": "3.0.0",
        "type": "lrs-extension",
        "author": "LRS Team",
        "description": "纯 cmd + start 新窗口：set /p 输入关键词，for /r 递归搜索，notepad 显示结果。无 PowerShell、无 JScript、无 base64。",
        "entry": "cmd.exe /c echo search",
        "points": {
            "topbar_buttons": [
                {
                    "id": "search-current",
                    "label": "🔍 搜索",
                    "command": make_cmd('"%D%"'),
                    "position": 10
                }
            ],
            "context_menu": [
                {
                    "id": "search-in-folder",
                    "label": "在此文件夹中搜索…",
                    "applyTo": "Folder",
                    "command": make_cmd('"%F%"'),
                },
                {
                    "id": "search-background",
                    "label": "在当前文件夹搜索…",
                    "applyTo": "Background",
                    "command": make_cmd('"%D%"'),
                },
                {
                    "id": "search-by-name",
                    "label": "搜索同名文件",
                    "applyTo": "File",
                    "command": make_cmd('"%F%"'),
                },
            ],
            "file_columns": [
                {
                    "id": "search_extension_col",
                    "header": "扩展名",
                    "value": "extension",
                    "width": 0.8
                },
                {
                    "id": "search_path_col",
                    "header": "完整路径",
                    "value": "path",
                    "width": 2.0
                }
            ],
            "settings": [
                {
                    "key": "maxResults",
                    "label": "最大结果数",
                    "type": "number",
                    "default": "1000"
                }
            ],
        },
    }


def main():
    hlds = make_hlds()
    text = json.dumps(hlds, ensure_ascii=False, indent=2)
    json.loads(text)  # validate
    out_path = '/workspace/ext/search.hlds'
    with open(out_path, 'w', encoding='utf-8') as f:
        f.write(text + '\n')
    print(f'Wrote {out_path} ({len(text)} chars)')

    cmd = hlds['points']['topbar_buttons'][0]['command']
    print()
    print('Topbar command (first 600 chars):')
    print(cmd[:600])
    print('...')
    print('Topbar command (last 200 chars):')
    print(cmd[-200:])


if __name__ == '__main__':
    main()
