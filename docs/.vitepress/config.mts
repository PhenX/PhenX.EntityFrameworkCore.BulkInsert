import {defineConfig} from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
    title: "PhenX EFCore BulkInsert",
    description: "Super fast bulk insert for EF Core",
    themeConfig: {
        outline: "deep",
        search: {
            provider: 'local'
        },

        // https://vitepress.dev/reference/default-theme-config
        nav: [
            {text: 'Home', link: '/'},
            {text: 'Documentation', link: '/documentation'},
        ],

        sidebar: [
            {
                text: 'Getting started',
                items: [
                    {text: 'Installation', link: '/getting-started#installation'},
                    {text: 'Usage', link: '/getting-started#usage'},
                ]
            },
            {
                text: 'Documentation',
                link: '/documentation'
            },
            {
                text: 'Limitations',
                link: '/limitations'
            },
        ],

        editLink: {
            pattern: 'https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/edit/main/README.md/edit/main/docs/:path',
            text: 'Edit this page on GitHub'
        },

        lastUpdated: {
            text: 'Updated at',
            formatOptions: {
                dateStyle: 'full',
                timeStyle: 'medium'
            }
        },

        socialLinks: [
            {
                icon: 'github', link: 'https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert',
            }
        ],

        externalLinkIcon: true,

        footer: {
            message: 'Released under the MIT License.',
            copyright: 'Copyright © 2025-present Fabien Ménager'
        }
    }
})
