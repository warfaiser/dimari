import sys, re, subprocess
from pathlib import Path
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

p = Parser(Language(tree_sitter_c_sharp.language()))
problems = []

def text(n, src): return src[n.start_byte:n.end_byte].decode()

def check_file(path):
    src = Path(path).read_bytes()
    tree = p.parse(src)
    if tree.root_node.has_error:
        problems.append((path, 'SYNTAX', tree.root_node.start_point))
        return
    # duplicate fields/properties/methods in the same class
    for cls in walk(tree.root_node, 'class_declaration'):
        names = {}
        body = cls.child_by_field_name('body')
        if body is None: continue
        for m in body.children:
            if m.type == 'method_declaration':
                n = m.child_by_field_name('name')
                if n:
                    key = (text(n, src), len([x for x in m.children if x.type == 'parameter_list'][0].named_children) if any(x.type=='parameter_list' for x in m.children) else 0)
                    names.setdefault(key, []).append(m.start_point[0]+1)
            elif m.type == 'property_declaration':
                n = m.child_by_field_name('name')
                if n:
                    key = ('prop', text(n, src))
                    names.setdefault(key, []).append(m.start_point[0]+1)
        for k, v in names.items():
            if len(v) > 1:
                problems.append((path, f'DUPLICATE {k} lines {v}', None))
    # obvious Quaternion/Vector3 mix-ups in calls of locally declared methods
    sigs = {}
    for md in walk(tree.root_node, 'method_declaration'):
        name = md.child_by_field_name('name')
        params = md.child_by_field_name('parameters')
        if not name or not params: continue
        types = []
        for prm in params.named_children:
            t = prm.child_by_field_name('type')
            if t: types.append(text(t, src))
        sigs.setdefault(text(name, src), []).append(types)
    for call in walk(tree.root_node, 'invocation_expression'):
        fn = call.child_by_field_name('function')
        if fn is None or fn.type != 'identifier': continue
        nm = text(fn, src)
        if nm not in sigs: continue
        args = call.child_by_field_name('arguments')
        if args is None: continue
        vals = [text(a, src) for a in args.named_children]
        for sig in sigs[nm]:
            if len(sig) != len(vals): continue
            for i, (want, got) in enumerate(zip(sig, vals)):
                if want in ('Quaternion',) and re.match(r'^(Vector3\.|new Vector3)', got):
                    problems.append((path, f'{nm} argument {i+1}: expected Quaternion, got {got[:40]} line {call.start_point[0]+1}', None))
                if want in ('Vector3',) and re.match(r'^(Quaternion\.|new Quaternion)', got):
                    problems.append((path, f'{nm} argument {i+1}: expected Vector3, got {got[:40]} line {call.start_point[0]+1}', None))

def walk(node, kind):
    if node.type == kind: yield node
    for c in node.children:
        yield from walk(c, kind)

for arg in sys.argv[1:]:
    check_file(arg)
for pr in problems: print('PROBLEM:', pr[0], pr[1], pr[2] or '')

# Локальные переменные, использованные вне своего блока, — это CS0103, который
# tree-sitter не считает синтаксической ошибкой. Проверяем отдельным скриптом.
scope = Path(__file__).with_name('check_scope.py')
if scope.exists() and sys.argv[1:]:
    result = subprocess.run([sys.executable, str(scope)] + sys.argv[1:], capture_output=True, text=True)
    sys.stdout.write(result.stdout)
    if result.returncode != 0:
        sys.exit(1)

sys.exit(1 if problems else 0)
