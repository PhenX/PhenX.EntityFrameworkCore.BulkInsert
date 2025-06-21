import {defineConfig} from 'vitepress';

const umamiScript: HeadConfig = ["script", {
    defer: "true",
    src: "https://cloud.umami.is/script.js",
    "data-website-id": "8bd6aedb-a830-4127-bd2c-abe704095784",
}]

const baseHeaders: HeadConfig[] = [];

const headers = process.env.GITHUB_PAGES === "true" ?
    [...baseHeaders, umamiScript] :
    baseHeaders;

// https://vitepress.dev/reference/site-config
export default defineConfig({
    title: "PhenX EFCore BulkInsert",
    description: "Super fast bulk insert for EF Core",
    base: '/PhenX.EntityFrameworkCore.BulkInsert/',
    head: headers,
    themeConfig: {
        outline: "deep",
        search: {
            provider: 'local',
        },

        // https://vitepress.dev/reference/default-theme-config
        nav: [
            {
                text: 'Home',
                link: '/',
            },
            {
                text: 'Getting started',
                link: '/getting-started',
            },
            {
                text: 'Documentation',
                link: '/documentation',
            },
            {
                text: 'Limitations',
                link: '/limitations',
            },
        ],

        sidebar: [
            {
                text: 'Getting started',
                link: '/getting-started',
            },
            {
                text: 'Documentation',
                link: '/documentation',
            },
            {
                text: 'Limitations',
                link: '/limitations',
            },
        ],

        editLink: {
            pattern: 'https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/edit/main/README.md/edit/main/docs/:path',
            text: 'Edit this page on GitHub',
        },

        lastUpdated: {
            text: 'Updated at',
            formatOptions: {
                dateStyle: 'full',
                timeStyle: 'medium',
            },
        },

        socialLinks: [
            {
                icon: 'github', link: 'https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert',
            },
        ],

        externalLinkIcon: true,

        footer: {
            message: 'Released under the MIT License.',
            copyright: 'Copyright © 2025-present Fabien Ménager',
        },
    }
})
