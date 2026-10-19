"""Finds locals used outside the block that declares them (a common CS0103 after refactors)."""
import sys
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

parser = Parser(Language(tree_sitter_c_sharp.language()))
METHODS = {"method_declaration", "constructor_declaration", "local_function_statement", "accessor_declaration"}
BLOCKS = {"block", "switch_section", "for_statement", "foreach_statement", "while_statement",
          "do_statement", "using_statement", "try_statement", "catch_clause", "if_statement", "else_clause",
          "lambda_expression"}


def txt(node, src):
    return src[node.start_byte:node.end_byte].decode("utf8", "ignore")


def is_member_name(node):
    """r.bounds.size / item.name — the right side of a member access is not a variable use."""
    parent = node.parent
    if parent is None:
        return False
    if parent.type in ("member_access_expression", "member_binding_expression", "qualified_name"):
        name = parent.child_by_field_name("name")
        return name is not None and node == name
    if parent.type == "assignment_expression":
        return node == parent.child_by_field_name("left")
    return False


def is_type_position(node):
    """Tuple element names and generic type arguments look like identifiers but are types."""
    n = node.parent
    while n is not None:
        if n.type in ("tuple_element", "type_argument_list", "tuple_type", "generic_name",
                      "array_type", "nullable_type", "implicit_type"):
            return True
        if n.type in ("block", "method_declaration", "local_declaration_statement"):
            return False
        n = n.parent
    return False


def variables_in(node):
    names = []
    if node.type == "variable_declarator":
        target = node.child_by_field_name("name")
        if target is not None:
            if target.type == "identifier":
                names.append(target)
            else:
                for child in target.children:
                    if child.type == "identifier":
                        names.append(child)
    elif node.type == "foreach_statement":
        left = node.child_by_field_name("left")
        if left is not None and left.type == "identifier":
            names.append(left)
    elif node.type == "lambda_expression":
        # Параметры лямбды принадлежат только её телу.
        for child in node.children:
            if child.type in ("identifier", "implicit_parameter"):
                names.append(child)
            elif child.type == "parameter_list":
                for parameter in child.children:
                    if parameter.type != "parameter":
                        continue
                    for part in parameter.children:
                        if part.type == "identifier":
                            names.append(part)
                            break
    elif node.type == "catch_declaration":
        for child in node.children:
            if child.type in ("identifier", "implicit_parameter"):
                names.append(child)
    return names


def check(path):
    src = open(path, "rb").read()
    tree = parser.parse(src)
    problems = []

    def enclosing_block(node, method):
        n = node.parent
        while n is not None and n != method and n.type not in BLOCKS:
            n = n.parent
        return n if n is not None else method

    def inside(node, block, method):
        if block == method:
            return True
        n = node
        while n is not None:
            if n == block:
                return True
            n = n.parent
        return False

    def scope(method):
        decls = {}
        def collect(node):
            for name in variables_in(node):
                decls.setdefault(txt(name, src), []).append(enclosing_block(node, method))
            for child in node.children:
                collect(child)
        collect(method)
        if not decls:
            return
        def use(node):
            if node.type == "identifier" and not is_member_name(node) and not is_type_position(node):
                name = txt(node, src)
                if name in decls and not any(inside(node, block, method) for block in decls[name]):
                    problems.append(f"{path}:{node.start_point[0] + 1}: «{name}» используется вне блока объявления (CS0103?)")
            for child in node.children:
                use(child)
        use(method)

    def walk(node):
        if node.type in METHODS:
            scope(node)
        for child in node.children:
            walk(child)

    walk(tree.root_node)
    return problems


failed = False
for path in sys.argv[1:]:
    for problem in check(path):
        print("SCOPE:", problem)
        failed = True
print("SCOPE CHECK:", "FAIL" if failed else "OK")
sys.exit(1 if failed else 0)
