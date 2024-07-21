const gulp = require('gulp');
const webpackStream = require('webpack-stream');
const webpack = require('webpack');
const path = require('path');

gulp.task('build-js', () => {
    return gulp.src('src/index.js')
        .pipe(webpackStream(require('./webpack.config.js'), webpack))
        .pipe(gulp.dest('wwwroot/js'));
});

gulp.task('default', gulp.series('build-js'));
