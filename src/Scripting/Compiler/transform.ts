import * as ts from "typescript";
import { Aliases } from "./alias";
 
let state = {
    inheritedClassName: "",
    aliases: <{ [className: string]: { [member: string]: string } }> { }
};

export default function(program: ts.Program, pluginOptions: {}) {
    return (ctx: ts.TransformationContext) => {
        return (sourceFile: ts.SourceFile) => {
            function visitClassDeclarationChildren(node: ts.Node): ts.Node {
                if (ts.isIdentifier(node))
                    return transformIndentifier(node);

                return ts.visitEachChild(node, visitClassDeclarationChildren, ctx);                
            }

            function visitClassDeclaration(node: ts.Node): ts.Node {
                if (ts.isHeritageClause(node)) {
                    state.inheritedClassName = findIdentifierInHeritageClause(node);
                    state.aliases[state.inheritedClassName] = getAliases(state.inheritedClassName);
                }

                return ts.visitEachChild(node, visitClassDeclarationChildren, ctx);
            }

            function visitNode(node: ts.Node): ts.Node {
                if (ts.isClassDeclaration) 
                    return ts.visitEachChild(node, visitClassDeclaration, ctx);

                return ts.visitEachChild(node, visitNode, ctx);
            }

            return ts.visitEachChild(sourceFile, visitNode, ctx);
        };
    };
}

function getAliases(className: string): { [key: string]: string } {
    let aliases: { [key: string]: string } = null;
    let delimiter = "->";
    for (let key in Aliases) {
        if (!key.endsWith(className)) continue;
        aliases = {};
        let splitKey = key.split(delimiter);
        while (splitKey.length > 0) {
            let newKey = splitKey.join(delimiter);
            if (Aliases[newKey]) aliases = { ...aliases, ...Aliases[newKey] };
            splitKey.pop();
        }
        return aliases;
    }
    return aliases;
}

function transformIndentifier(node: ts.Identifier): ts.Identifier {
    if (!state.aliases[state.inheritedClassName]) return node;
    return ts.createIdentifier(state.aliases[state.inheritedClassName][node.text] || node.text);
}

function findIdentifierInHeritageClause(node: ts.Node): string {
    for (let child of node.getChildren()) {
        if (child.kind == ts.SyntaxKind.SyntaxList)
            return child.getText();
    }
    return "";
}

function log(node: ts.Node): void {
    console.log(`${node.getText()} (kind: ${node.kind})`);
    console.log()
    console.log()
}