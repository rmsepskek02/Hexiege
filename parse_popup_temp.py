
import re, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

with open('Assets/_Project/Scenes/Game.unity', 'r', encoding='utf-8') as f:
    content = f.read()

go_rt_map = re.compile(r'--- !u!224 &(\d+)\nRectTransform:.*?m_GameObject: \{fileID: (\d+)\}', re.DOTALL)
rt_to_go = {m.group(1): m.group(2) for m in go_rt_map.finditer(content)}

go_name_map = re.compile(r'--- !u!1 &(\d+)\nGameObject:.*?m_Name: (.+?)\n', re.DOTALL)
go_names = {m.group(1): m.group(2).strip() for m in go_name_map.finditer(content)}

mb_go_map = re.compile(r'--- !u!114 &(\d+)\nMonoBehaviour:.*?m_GameObject: \{fileID: (\d+)\}', re.DOTALL)
mb_to_go = {m.group(1): m.group(2) for m in mb_go_map.finditer(content)}

rt_tree = re.compile(r'--- !u!224 &(\d+)\nRectTransform:(.*?)(?=--- !u!)', re.DOTALL)
rt_data = {}
for m in rt_tree.finditer(content):
    rt_id = m.group(1)
    b = m.group(2)
    amin = re.search(r'm_AnchorMin: \{x: ([\d.\-]+), y: ([\d.\-]+)\}', b)
    amax = re.search(r'm_AnchorMax: \{x: ([\d.\-]+), y: ([\d.\-]+)\}', b)
    pos  = re.search(r'm_AnchoredPosition: \{x: ([\d.\-]+), y: ([\d.\-]+)\}', b)
    size = re.search(r'm_SizeDelta: \{x: ([\d.\-]+), y: ([\d.\-]+)\}', b)
    pivot = re.search(r'm_Pivot: \{x: ([\d.\-]+), y: ([\d.\-]+)\}', b)
    children_m = re.search(r'm_Children:(.*?)m_Father:', b, re.DOTALL)
    children = re.findall(r'fileID: (\d+)', children_m.group(1)) if children_m else []
    rt_data[rt_id] = {
        'go': rt_to_go.get(rt_id,''),
        'amin': (amin.group(1), amin.group(2)) if amin else ('?','?'),
        'amax': (amax.group(1), amax.group(2)) if amax else ('?','?'),
        'pos': (pos.group(1), pos.group(2)) if pos else ('?','?'),
        'size': (size.group(1), size.group(2)) if size else ('?','?'),
        'pivot': (pivot.group(1), pivot.group(2)) if pivot else ('?','?'),
        'children': children
    }

mb_full = re.compile(r'--- !u!114 &(\d+)\nMonoBehaviour:(.*?)(?=--- !u!)', re.DOTALL)
mb_data = {}
for m in mb_full.finditer(content):
    mb_id = m.group(1)
    b = m.group(2)
    go_id = mb_to_go.get(mb_id,'')
    cls_m = re.search(r'm_EditorClassIdentifier: (.+)', b)
    if not cls_m:
        continue
    c = cls_m.group(1).strip()
    if go_id not in mb_data:
        mb_data[go_id] = []
    info = {'cls': c}
    if 'LayoutGroup' in c:
        pad_l = re.search(r'm_Left: (-?\d+)', b)
        pad_r = re.search(r'm_Right: (-?\d+)', b)
        pad_t = re.search(r'm_Top: (-?\d+)', b)
        pad_b_m = re.search(r'm_Bottom: (-?\d+)', b)
        spacing = re.search(r'm_Spacing: ([\d.\-]+)', b)
        ctrl_w = re.search(r'm_ChildControlWidth: (\d)', b)
        ctrl_h = re.search(r'm_ChildControlHeight: (\d)', b)
        force_w = re.search(r'm_ChildForceExpandWidth: (\d)', b)
        force_h = re.search(r'm_ChildForceExpandHeight: (\d)', b)
        align = re.search(r'm_ChildAlignment: (\d+)', b)
        info.update({
            'pad': (pad_l.group(1) if pad_l else '?', pad_r.group(1) if pad_r else '?',
                    pad_t.group(1) if pad_t else '?', pad_b_m.group(1) if pad_b_m else '?'),
            'spacing': spacing.group(1) if spacing else '?',
            'ctrl': (ctrl_w.group(1) if ctrl_w else '?', ctrl_h.group(1) if ctrl_h else '?'),
            'force': (force_w.group(1) if force_w else '?', force_h.group(1) if force_h else '?'),
            'align': align.group(1) if align else '?'
        })
    elif 'LayoutElement' in c:
        ignore_m = re.search(r'm_IgnoreLayout: (\d)', b)
        flexW_m = re.search(r'm_FlexibleWidth: ([\d.\-]+)', b)
        flexH_m = re.search(r'm_FlexibleHeight: ([\d.\-]+)', b)
        minW_m = re.search(r'm_MinWidth: ([\d.\-]+)', b)
        minH_m = re.search(r'm_MinHeight: ([\d.\-]+)', b)
        prefW_m = re.search(r'm_PreferredWidth: ([\d.\-]+)', b)
        prefH_m = re.search(r'm_PreferredHeight: ([\d.\-]+)', b)
        info.update({
            'ignore': ignore_m.group(1) if ignore_m else '?',
            'flexW': flexW_m.group(1) if flexW_m else '?',
            'flexH': flexH_m.group(1) if flexH_m else '?',
            'minW': minW_m.group(1) if minW_m else '?',
            'minH': minH_m.group(1) if minH_m else '?',
            'prefW': prefW_m.group(1) if prefW_m else '?',
            'prefH': prefH_m.group(1) if prefH_m else '?',
        })
    elif 'Image' in c and 'Layout' not in c:
        sprite_m = re.search(r'm_Sprite: \{fileID: ([\d\-]+)', b)
        preserve_m = re.search(r'm_PreserveAspect: (\d)', b)
        raycast_m = re.search(r'm_RaycastTarget: (\d)', b)
        sprite_id = sprite_m.group(1) if sprite_m else '0'
        has_sprite = sprite_id not in ['0', '-1', '?'] and sprite_id != '0'
        info.update({
            'sprite': 'YES' if has_sprite else 'NONE',
            'preserve': preserve_m.group(1) if preserve_m else '?',
            'raycast': raycast_m.group(1) if raycast_m else '?'
        })
    elif 'CanvasGroup' in c:
        alpha_m = re.search(r'm_Alpha: ([\d.]+)', b)
        interact_m = re.search(r'm_Interactable: (\d)', b)
        blocks_m = re.search(r'm_BlocksRaycasts: (\d)', b)
        info.update({
            'alpha': alpha_m.group(1) if alpha_m else '?',
            'interact': interact_m.group(1) if interact_m else '?',
            'blocks': blocks_m.group(1) if blocks_m else '?'
        })
    elif 'TextMeshPro' in c:
        font_m = re.search(r'm_fontAsset: \{fileID: ([\d\-]+)', b)
        info.update({'font': font_m.group(1) if font_m else '0'})
    mb_data[go_id].append(info)

align_map = {'0':'UpperLeft','1':'UpperCenter','2':'UpperRight','3':'MiddleLeft',
             '4':'MiddleCenter','5':'MiddleRight','6':'LowerLeft','7':'LowerCenter','8':'LowerRight'}

def get_comps(go_id):
    return mb_data.get(go_id, [])

def print_node(rt_id, depth=0):
    d = rt_data.get(rt_id)
    if not d:
        return
    go_id = d['go']
    name = go_names.get(go_id, f'[{go_id}]')
    indent = '  ' * depth
    amin = d['amin']
    amax = d['amax']
    pos = d['pos']
    size = d['size']
    pivot = d['pivot']

    is_fixed_size = (
        size[0] not in ('0','0.0') or size[1] not in ('0','0.0')
    ) and not (
        amin[0] != amax[0] or amin[1] != amax[1]
    )

    print(f'{indent}[{name}]')
    print(f'{indent}  RT: min=({amin[0]},{amin[1]}) max=({amax[0]},{amax[1]}) pos=({pos[0]},{pos[1]}) size=({size[0]},{size[1]}) pivot=({pivot[0]},{pivot[1]})')

    for comp in get_comps(go_id):
        cls = comp['cls']
        if 'LayoutGroup' in cls:
            lg = 'HLG' if 'Horizontal' in cls else 'VLG'
            pad = comp['pad']
            has_fixed_pad = any(p not in ('0','?') for p in pad)
            pad_str = f'L={pad[0]} R={pad[1]} T={pad[2]} B={pad[3]}'
            print(f'{indent}  [{lg}] pad=({pad_str}) spacing={comp["spacing"]} ctrl=({comp["ctrl"][0]},{comp["ctrl"][1]}) force=({comp["force"][0]},{comp["force"][1]}) align={align_map.get(comp["align"],comp["align"])}')
        elif 'LayoutElement' in cls:
            print(f'{indent}  [LE] ignore={comp["ignore"]} flex=({comp["flexW"]},{comp["flexH"]}) min=({comp["minW"]},{comp["minH"]}) pref=({comp["prefW"]},{comp["prefH"]})')
        elif 'Image' in cls and 'Layout' not in cls:
            print(f'{indent}  [Image] sprite={comp["sprite"]} preserve={comp["preserve"]} raycast={comp["raycast"]}')
        elif 'CanvasGroup' in cls:
            print(f'{indent}  [CG] alpha={comp["alpha"]} interact={comp["interact"]} blocks={comp["blocks"]}')
        elif 'TextMeshPro' in cls:
            print(f'{indent}  [TMP] font={comp["font"]}')

    for child_rt in d['children']:
        print_node(child_rt, depth+1)

print_node('1959302709')
